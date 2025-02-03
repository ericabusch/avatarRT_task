"""
avatarRT.py
Drive the real-time BCI experiment.

"""
import warnings
warnings.filterwarnings("ignore", category=FutureWarning)
warnings.filterwarnings("ignore", category=UserWarning)

import logging
import os, sys, glob, argparse, warnings
import tempfile
import numpy as np
import nibabel as nib
import scipy.io as io
import time, json, tempfile
from nibabel.processing import smooth_image
from configure_config import configure_config_projectInterface
from nibabel.nicom import dicomreaders
from subprocess import call

# setup paths
tmpPath = tempfile.gettempdir()
currPath = os.path.dirname(os.path.realpath(__file__))
rootPath = os.path.dirname(os.path.dirname(currPath))

# add MRAE and TPHATE to paths
pkgPath = os.path.join(currPath, 'analysis_pkgs')
sys.path.append(rootPath)
sys.path.append(pkgPath)

import avatarRT_utils
from avatarRT_utils import load_model_from_cfg
from MRAE import mrae
from MRAE.dataHandler import TestDataset
from nilearn.masking import apply_mask
# import project modules from rt-cloud
from rtCommon.utils import loadConfigFile, stringPartialFormat
from rtCommon.clientInterface import ClientInterface
from rtCommon.dataInterface import DataInterface
from rtCommon.imageHandling import readRetryDicomFromDataInterface, convertDicomImgToNifti, saveAsNiftiImage
from rtCommon.bidsArchive import BidsArchive
from rtCommon.bidsRun import BidsRun

logLevel = logging.INFO
useInitWatch = False

# global verbose
flirt_params = '-bins 256 -cost corratio -searchrx 0 0 -searchry 0 0 -searchrz 0 0 -dof 6 -interp nearestneighbour'
def build_argparser():
    global verbose, useInitWatch, args, defaultConfig
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', '-c', default='sample.toml', type=str,
                           help='experiment config file (.json or .toml)')
    parser.add_argument('--runs', '-r', default='', type=str,
                           help='Comma separated list of run numbers')
    parser.add_argument('--scans', '-s', default='', type=str,
                           help='Comma separated list of scan numbers')
    parser.add_argument('--useInitWatch', '-w', default=False, action='store_true',
                           help='use initWatch() functions instead of stream functions')
    parser.add_argument('--debugging', '-d', default=True, action='store_true',
                           help='print verbose output')
    parser.add_argument('--verbose', '-v', default=True, action='store_true',
                           help='print verbose output')
    parser.add_argument('--isSynthetic',  default=False, action='store_true',
                           help='running with synthetic data')
    
    return parser


def doRuns(cfg, dataInterface, subjInterface, webInterface, bidsInterface, archive, timeout=300):
    global mask, verbose, synthetic
    scanNum=cfg.scanNum[0]
    runNum=cfg.runNum[0]
    total_TRs = cfg.num_total_TRs
    bids_root = f'{cfg.subjectName}_run_{runNum:02d}_'
    RUN_OUTDIR = f'{cfg.thisSubjectsDir}/ses_{cfg.session:02d}/data'
    if cfg.replay == 1:
        RUN_OUTDIR+=f'_replay'
        cfg.TR = 0.1
        print(f"Replaying data! Set tr={cfg.TR}, saving to {RUN_OUTDIR}")
        bids_root+=f'_component_{cfg.perturbation:02d}'
    os.makedirs(RUN_OUTDIR, exist_ok=True)
    THIS_MASK = f'{cfg.maskToday}_run_{runNum:02d}_mask.nii.gz'
    
    this_mrae, MAPPING_SLOPE, MAPPING_INTERCEPT, MANI_COMP = load_model_from_cfg(cfg)
    if verbose: print(f"Loaded MRAE from {cfg.modelDir}")
    if verbose: print(f'Running perturbation type: {cfg.perturbation}')
    MAP_ANGLE = lambda x : MAPPING_SLOPE * x + MAPPING_INTERCEPT
    INIT_MTX = lambda a,b : np.full((a, b), np.nan)

    if synthetic:
        cfg.dicomDir = cfg.syntheticDataPath
        cfg.dicom_dir = cfg.syntheticDataPath

    if verbose: print(f"Scan={scanNum}, run={runNum}, dicoms from {cfg.dicomDir}")
    while not os.path.isdir(cfg.dicomDir):
        time.sleep(0.00001)    
    ############################# Set up how we're going to get files ################################
    
    # this means we're streaming dicoms
    if cfg.dsAccessionNumber == "None":
        dicomScanNamePattern = stringPartialFormat(cfg.dicomNamePattern, 'SCAN', scanNum)
        if verbose: print(f'Scan name pattern: {dicomScanNamePattern}')
        streamId = bidsInterface.initDicomBidsStream(cfg.dicomDir, dicomScanNamePattern,
                                                     cfg.minExpectedDicomSize,
                                                     anonymize=True,
                                                     **{'subject': cfg.LASTNAME,
                                                        'run': f"{runNum:02d}",
                                                        'task': cfg.taskName})
    # this means we're doing open neuro replay
    else:
        # For OpenNeuro replay, initialize a BIDS stream using the dataset's Accession Number
        streamId = bidsInterface.initOpenNeuroStream(cfg.dsAccessionNumber,
                                                     **{'subject': cfg.subjectNum,
                                                        'run': f"0{runNum}",
                                                        'task': cfg.taskName})
    
    # prep BIDS-Run, which will store each BIDS-Incremental in the current run
    currentBidsRun = BidsRun()

    ############################## Set up to plot data on the web interface ############################
    if verbose: print(" - Clearing any pre-existing plot for this run")
    webInterface.clearRunPlot(runNum)
    
    allowedFileTypes = dataInterface.getAllowedFileTypes()
    if verbose: print(f"Allowed file types: {allowedFileTypes} | dicoms from {cfg.dicomDir}")

    dataInterface.initWatch(cfg.dicomDir,
                            cfg.dicomNamePattern,
                            cfg.minExpectedDicomSize)
 
    mask = nib.load(cfg.maskOrig).get_fdata()
    print(f"Number of voxels in mask: {np.sum(mask == 1)}; mask of shape {mask.shape}")
        
    # initialize projected timeseries
    projected_timeseries = INIT_MTX(total_TRs, this_mrae.manifold_dim)    
    # this is going to have the bottleneck embeddings
    embedded_timeseries = INIT_MTX(total_TRs, this_mrae.manifold_dim)
    # initialized non-normalized data to keep track of
    masked_raw_data = INIT_MTX(total_TRs, this_mrae.IO_dim)
    # initialize normalized data to track
    masked_data = INIT_MTX(total_TRs, this_mrae.IO_dim)    
    # initialize decoded angles (which are bound [-1, 1])
    angles = INIT_MTX(total_TRs, 1).squeeze()
    # keep track of preprocessing and analysis timing
    preproc_times = INIT_MTX(total_TRs, 1).squeeze()
    analysis_times = INIT_MTX(total_TRs, 1).squeeze()

    confidence = cfg.staircasedBrainConfidence # changes with each run
    TEMP_DIR=cfg.TempDir    
    ref_fn = cfg.templateFuncVol
    THIS_REF_VOL=f'{cfg.thisSubjectsDir}/reference/ses_{cfg.session:02d}_run_{runNum:02d}_reference_volume.nii'
    
    VOXEL_MEAN, VOXEL_STD, targetNVox, TMAT_HERE2SES1  = None, None, None, None # this will get filled in
    print(" ")   
    print(f'===========STARTING RUN===========')
    print(" ")
    for current_TR in np.arange(1, total_TRs+1): # to account for DICOM start at 1
        t0 = time.time()
        if current_TR == 1: dicomTimeout = timeout
        else: dicomTimeout = cfg.TR*2
        
        # catch if there aren't any more timepoints coming in and return/archive scan.
        try:
            bidsIncremental = bidsInterface.getIncremental(streamId, 
                                                           volIdx=current_TR,
                                                           demoStep=cfg.demoStep,
                                                          timeout=dicomTimeout)
        except:
            if verbose: print(f"No more images coming in at {current_TR}; exiting")
            break
        
        currentBidsRun.appendIncremental(bidsIncremental)
        # this process returns a nifti image, regardless if it is from the dicom stream or open neuro stream
        niftiObject  = bidsIncremental.image
        if current_TR == 1: # align the ROI to today's first vol
            nib.save(niftiObject, THIS_REF_VOL) # save first volume as this run's reference

            # Smooth this run's reference volume to match the SES 01 reference
            REF_SMOOTH= f'{cfg.thisSubjectsDir}/reference/ses_{cfg.session:02d}_run_{runNum:02d}_reference_volume_smoothed.nii'
            command = f"fslmaths {THIS_REF_VOL} -kernel gauss {cfg.fwhm / 2.3548} -fmean {REF_SMOOTH}"
            #if verbose: print(command)
            call(command, shell=True)
            
            # Align SES N reference volume to SES 01 reference; save the transform in TMAT_HERE2SES1 (Reference N -> Reference S1)
            ORIG_REF_VOL=f'{cfg.thisSubjectsDir}/reference/ses_01_example_func.nii.gz' # this is the space mask.nii.gz is in
            temp_string = THIS_REF_VOL.replace('.nii', '_to_ses_01_space')
            TMAT_HERE2SES1 = f'{temp_string}.mat'
            TEMP_FIN = f'{TEMP_DIR}/temp_final_step.nii.gz'
            command = f'flirt -in {REF_SMOOTH} -ref {ORIG_REF_VOL} -out {TEMP_FIN} -omat {TMAT_HERE2SES1} {flirt_params}'
            call(command, shell=True)

        else:
            # These steps are needed only if it's not the first TR
            # save the nifti to temporary location
            temp_fn=f'{TEMP_DIR}/temp.nii'
            nib.save(niftiObject, temp_fn)
            
            # spatial smoothing FWHM kernel (dividing by 2.3548 converts from stdv. to fwhm)
            command = f"fslmaths {TEMP_DIR+'/temp.nii'} -kernel gauss {cfg.fwhm / 2.3548} -fmean {TEMP_DIR+'/temp_smoothed.nii'}"
            call(command, shell=True)
            
            # run motion correction to this run's functional reference smoothed 
            command = f"mcflirt -in {TEMP_DIR+'/temp_smoothed.nii.gz'} -reffile {REF_SMOOTH} -out {TEMP_DIR+'/temp_smoothed_MC_to_ref_smooth.nii.gz'}"
            call(command, shell=True)
            
            # now apply transform from functional reference smoothed -> ses 1 space
            # uses -applyxfm to reuse the previously defined transformation (only takes refvol to determine size of output volume)
            command = f"flirt -in {TEMP_DIR+'/temp_smoothed_MC_to_ref_smooth.nii.gz'}  -ref {ORIG_REF_VOL} -out {TEMP_DIR+'/temp_final_step.nii.gz'} -init {TMAT_HERE2SES1} -applyxfm -interp nearestneighbour"
            call(command, shell=True)
        
        # load back in the data and then mask and normalize
        niiData = nib.load(f'{TEMP_DIR}/temp_final_step.nii.gz')
        niiData = niiData.get_fdata()
        thisTR_data = niiData[mask == 1]
        masked_raw_data[current_TR-1] = thisTR_data
        
        # return nothing for now; continue
        if current_TR < cfg.Calibration_TRs-1:
            if verbose: print(f'Aggregating normalization data; TR={current_TR}')
            subjInterface.setResult(runId=runNum, trId=current_TR, value=np.nan)
            webInterface.plotDataPoint(runNum, int(current_TR), np.nan)
            
        elif current_TR == cfg.Calibration_TRs-1:
            data2norm = masked_raw_data[:current_TR, :]
            VOXEL_MEAN, VOXEL_STD = np.nan_to_num(np.nanmean(data2norm, axis=0)), np.nan_to_num(np.nanstd(data2norm, axis=0))
            if verbose: print(f"computed mu={np.round(np.nanmean(VOXEL_MEAN),4)}, std={np.round(np.nanmean(VOXEL_STD),4)}; starting feedback trials on next TR!")
            masked_data[:current_TR] = (data2norm - VOXEL_MEAN) / VOXEL_STD
            preproc_time=time.time()-t0
            if verbose: print(f"all preproc done & save @ t={np.round(preproc_time,4)}")
            subjInterface.setResult(runId=runNum, trId=current_TR, value=np.nan)
            
        else:
            VOXEL_MEAN = np.nan_to_num(np.nanmean(masked_raw_data[:current_TR, :], axis=0))
            VOXEL_STD = np.nan_to_num(np.nanstd(masked_raw_data[:current_TR, :], axis=0))
            data_preproc = (thisTR_data - VOXEL_MEAN) / VOXEL_STD
            masked_data[current_TR-1, :] = data_preproc

            ############################### ANALYSIS #############################################
            # project new TR onto manifold
            projected_point = this_mrae.extract_projection_to_manifold(TestDataset(data_preproc))
            #if verbose: print(f"Manifold projection @ t={np.round(time.time() - t0, 4)}")
            # get the projected point loaded onto the MANI_COMP
            loading = np.dot(projected_point, MANI_COMP.T)[0]
            # map projection to angle
            decoded_angle = np.clip(MAP_ANGLE(loading), -1, 1)
            # Save this to subject interface
            subjInterface.setResult(runId=runNum, trId=current_TR, value=decoded_angle)
            if verbose: print(f'Passed result: {current_TR} | decoded: {np.round(decoded_angle,4)} | t={np.round(time.time() - t0,4)}')
            projected_timeseries[current_TR-1] = projected_point
            angles[current_TR-1] = decoded_angle
            preproc_times[current_TR-1] = preproc_time
            webInterface.plotDataPoint(runNum, int(current_TR), decoded_angle)
            analysis_times[current_TR-1] = time.time() - t0

    print(f"== END OF RUN {runNum}! ==\n")
    archive.appendBidsRun(currentBidsRun)
    bidsInterface.closeStream(streamId)
    np.save(os.path.join(RUN_OUTDIR, f'{bids_root}projected_data.npy'), projected_timeseries[:current_TR-1,:])
    np.save(os.path.join(RUN_OUTDIR, f'{bids_root}decoded_angles.npy'), angles[:current_TR-1])
    np.save(os.path.join(RUN_OUTDIR, f'{bids_root}analysis_times.npy'), analysis_times[:current_TR-1])
    np.save(os.path.join(RUN_OUTDIR, f'{bids_root}preproc_times.npy'), preproc_times[:current_TR-1])
    np.save(os.path.join(RUN_OUTDIR, f'{bids_root}masked_data.npy'), masked_data[:current_TR-1])
    np.save(os.path.join(RUN_OUTDIR, f'{bids_root}masked_raw_data.npy'), masked_raw_data[:current_TR-1])
    np.save(os.path.join(RUN_OUTDIR, f'{bids_root}embedded_timeseries.npy'), embedded_timeseries[:current_TR-1])
    print(f'Saved to {RUN_OUTDIR}/{bids_root}')
    return

def main():
    global verbose, useInitWatch, cfg, synthetic
    args = build_argparser().parse_args()
    verbose = args.verbose
    useInitWatch = args.useInitWatch
    cfg = configure_config_projectInterface(args.config, verbose)    
    synthetic = cfg.isSynthetic
    
    # initialize connections
    clientInterfaces = ClientInterface()
    subjInterface = clientInterfaces.subjInterface
    webInterface = clientInterfaces.webInterface
    bidsInterface = clientInterfaces.bidsInterface
    bids_outdir = f'{cfg.projectDir}/experiment/bidsDataset'
    archive = BidsArchive(bids_outdir)
    dataInterface = DataInterface(dataRemote=False, allowedDirs=['*'], allowedFileTypes=['*'])  # Create an instance of local datainterface
    if verbose: print("Starting runs....")

    doRuns(cfg, dataInterface, subjInterface, webInterface, bidsInterface, archive)
    return 0

if __name__ == "__main__":
    
    logger = logging.getLogger()
    logger.setLevel(logLevel)
    r = main()
    sys.exit(r)


