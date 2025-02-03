import warnings
warnings.filterwarnings("ignore", category=FutureWarning)
warnings.filterwarnings("ignore", category=UserWarning)
import os,subprocess, sys
currPath = os.path.dirname(os.path.realpath(__file__))
# obtain full path for root directory: '.../rt-cloud'
rootPath = os.path.dirname(os.path.dirname(currPath))
# add MRAE and TPHATE to paths
pkgPath = os.path.join(rootPath, 'projects', 'avatarRT', 'analysis_pkgs')
sys.path.append(rootPath)
sys.path.append(pkgPath)
import rtCommon.utils as utils
import random, shutil
import numpy as np
import glob

quest_parameters = {"startVal" : 0.5,
                "startValSd" : 2,
                "pThreshold" : 0.82,
                "nTrials" : 100,
                "minValue" : 0,
                "maxValue" : 1,
                "range":1}

game_modes = {1: "AutoMove",
                  2: "KeyboardMove",
                  3: "ScannerMove",
                  4: "JoystickMove"}
game_mode_mapping = game_modes

def configure_config_subjectPresentation(cfg_filename, this_run, verbose=False):
    if verbose: import pprint
    thisComputer = ""
    if "watts" in os.getcwd():
        rtpath = f"/home/watts/Desktop/ntblab/erica/"
        thisComputer = "Scanner"
    elif "milgram" in os.getcwd():
        rtpath = f"/gpfs/milgram/project/turk-browne/projects/elb77/BCI/"
        thisComputer = "Milgram"
    elif "elb" in os.getcwd():
        rtpath = "/Users/elb/Desktop/BCI/"
        thisComputer = "Laptop"
    elif 'ericabusch' in os.getcwd():
        rtpath = "/Users/ericabusch/Desktop/BCI/"
        thisComputer = "Tars"
    else:
        raise Exception("Config path error")

    cfg = utils.loadConfigFile(cfg_filename)

    if cfg.server == None:
        if thisComputer == "Scanner":
            cfg.server = f'{cfg.projInterfaceHost}:{cfg.projInterfacePort}'
        elif thisComputer == "Laptop":
            cfg.server = f'localhost:{cfg.projInterfacePort}'
        elif thisComputer == 'Tars':
            cfg.server = f'localhost:{cfg.projInterfacePort}'

    if verbose: print(f"connecting to {cfg.server}")

    cfg.projectDir = f'{rtpath}/rt-cloud/projects/avatarRT'
    cfg.subjectsDir = f'{cfg.projectDir}/experiment/subjects'
    cfg.thisSubjectsDir = f'{cfg.subjectsDir}/{cfg.subjectName}'
    cfg.run_outdir = f'{cfg.thisSubjectsDir}/ses_{cfg.session:02d}/behav/run_{this_run:03d}/'
    if os.path.exists(cfg.run_outdir):
        input(f"{cfg.run_outdir} already exists. Quit to change run; enter to continue. DO NOT PRESS GO ON UNITY YET!!!!!!!!!!!")

    os.makedirs(cfg.run_outdir, exist_ok=True)
    os.makedirs(cfg.run_outdir+"/scanner_comms/", exist_ok=True)
    # Set paths and parameters for this experiment
    if "watts" in os.getcwd():
        default_experiment_dir = "/home/watts/Desktop/ntblab/erica/RT_Avatar/experiment/"
        filename = "params.txt"
        DEFAULT_HOST = "127.0.1.1"#"192.168.137.5"
        KEYBOARD_NAME = 'Dell Dell Universal Receiver'  # 'Current Designs, Inc. 932'
        JOYSTICK_NAME = 'Current Designs, Inc. 932'  # insert this for polling
    else:
        default_experiment_dir = f'{rtpath}/RT_Avatar/experiment/'
        filename = "params2.txt"
        DEFAULT_HOST = 'localhost'
        KEYBOARD_NAME = 'Apple Internal Keyboard / Trackpad'
        JOYSTICK_NAME = ''
    cfg.JOYSTICK_NAME=JOYSTICK_NAME
    cfg.KEYBOARD_NAME=KEYBOARD_NAME
    cfg.UNITY_HOST=DEFAULT_HOST
    cfg.UNITY_PARAM_FN=filename
    cfg.DEFAULT_EXPERIMENT_DIR=default_experiment_dir
    # cfg.experiment_dir = f'{default_experiment_dir}/'

    game_modes = {1: "AutoMove",
                  2: "KeyboardMove",
                  3: "ScannerMove",
                  4: "JoystickMove"}
    cfg.GameMode = game_modes[cfg.game_mode]
    cfg.difficulty=cfg.runNums[this_run]
    if cfg.GameMode == "ScannerMove":
        cfg.QuestParameters = quest_parameters
    #if verbose: pprint.pprint(cfg)

    if verbose:
        print(f'Function configure_config_subjectPresentation')
        print(f'Run outdir: {cfg.run_outdir}')
        print(f'Scanner comms: {cfg.run_outdir}/scanner_comms/')

    return cfg

def configure_config_projectInterface(cfg_filename, verbose=True):
    if verbose: import pprint
    thisComputer = ""
    if "watts" in os.getcwd():
        rtpath = f"/home/watts/Desktop/ntblab/erica/"
        thisComputer = "Scanner"
    elif "milgram" in os.getcwd():
        rtpath = f"/gpfs/milgram/project/turk-browne/users/elb77/BCI/"
        thisComputer = "Milgram"
    elif "Unity" in os.getcwd():
        rtpath="/Volumes/GoogleDrive/My\ Drive/DriveLocal/Projects/NTB/BCI_scripts/"
        thisComputer = "Laptop"
    else:
        raise Exception("Config path error")
    if verbose: print(cfg_filename)
    cfg = utils.loadConfigFile(cfg_filename)
    if verbose: print(f"SUBJECT: {cfg.subjectName}")
    cfg.projectDir = f'{rtpath}/rt-cloud/projects/avatarRT'
    cfg.subjectsDir = f'{cfg.projectDir}/experiment/subjects'
    cfg.thisSubjectsDir = f'{cfg.subjectsDir}/{cfg.subjectName}'
    prev = cfg.session-1
    if prev==0: prev = 1
        
    # this is to use the model from day 1
    if cfg.modelDir == "None":
        cfg.modelDir = f'{cfg.thisSubjectsDir}/model/'
        if verbose: print(f'cfg modelDir was None, now is {cfg.modelDir}')
    
    
    if cfg.dsAccessionNumber != "None":
        dicom_basedir = '/tmp/openneuro/'
        dicom_pattern = f'{dicom_basedir}/{cfg.dsAccessionNumber}/sub-{cfg.subjectNum}/func/'
        cfg.TR = cfg.demoStep
    else:
        dicom_basedir = "/gpfs/milgram/project/realtime/DICOM/"
        #/gpfs/milgram/project/realtime/DICOM/{YYYYMMDD}.{rtFolder_subjectName}.{LASTNAME}
        dicom_pattern = f'{dicom_basedir}/{cfg.YYYYMMDD}.{cfg.rtFolder_subjectName}.{cfg.LASTNAME}*/'
        cfg.demoStep = cfg.TR
    
    dns = glob.glob(dicom_pattern)
    if len(dns) == 0:
        dicom_dirname = dicom_pattern.replace("*", "")
    else:
        dicom_dirname = dns[0]
    
    if cfg.dicomDir == "None":
        cfg.dicomDir = dicom_dirname
        if verbose: print(f'cfg dicomDir was None, now is {cfg.dicomDir}')

    cfg.maskOrig = f'{cfg.thisSubjectsDir}/reference/{cfg.mask}'
    cfg.maskToday = f'{cfg.thisSubjectsDir}/reference/ses_{cfg.session:02d}'
    # make folder structure
    os.makedirs(cfg.thisSubjectsDir, exist_ok=True)
    os.makedirs(f'{cfg.thisSubjectsDir}/reference', exist_ok=True)
#     cfg.TempDir = f' /gpfs/milgram/scratch60/turk-browne/elb77/REALTIME'
    for s in range(1,6):
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}', exist_ok=True)
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}/func', exist_ok=True)
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}/anat', exist_ok=True)
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}/fmap', exist_ok=True)
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}/behav', exist_ok=True)

    #cfg.TempDir = f'{cfg.TempDir}/{cfg.subjectName}/ses-{cfg.session:02d}'
    if os.path.exists(cfg.TempDir) and os.path.isdir(cfg.TempDir):
        shutil.rmtree(cfg.TempDir)
    os.makedirs(cfg.TempDir)
    
    if verbose:
        print("In function configure_config_projectInterface:")
        print(f'This subject dir: {cfg.thisSubjectsDir}')
        print(f'This session dir: {cfg.thisSubjectsDir}/ses_{cfg.session:02d}')
        print(f"This temp dir: {cfg.TempDir}")
        print(f"Today's mask: {cfg.maskToday}")
        print(f"Template volume: {cfg.templateFuncVol}")
        print(f'Dicom pattern: {dicom_pattern}')
        print(f"Dicom dir: {cfg.dicomDir}")

    return cfg

def configure_config_projectInterfaceOffline(cfg_filename, runNum, scanNum, verbose=True):
    if verbose: import pprint
    thisComputer = ""
    if "watts" in os.getcwd():
        rtpath = f"/home/watts/Desktop/ntblab/erica/"
        thisComputer = "Scanner"
    elif "milgram" in os.getcwd():
        rtpath = f"/gpfs/milgram/project/turk-browne/users/elb77/BCI/"
        thisComputer = "Milgram"
    elif "Unity" in os.getcwd():
        rtpath="/Volumes/GoogleDrive/My\ Drive/DriveLocal/Projects/NTB/BCI_scripts/"
        thisComputer = "Laptop"
    else:
        raise Exception("Config path error")
    if verbose: print(cfg_filename)
    cfg = utils.loadConfigFile(cfg_filename)
    if verbose: print(f"SUBJECT: {cfg.subjectName}")
    cfg.projectDir = f'{rtpath}/rt-cloud/projects/avatarRT'
    cfg.subjectsDir = f'{cfg.projectDir}/experiment/subjects'
    cfg.thisSubjectsDir = f'{cfg.subjectsDir}/{cfg.subjectName}'
    cfg.scanNum = scanNum
    cfg.runNum = runNum
    
    prev = cfg.session-1
    if prev==0: prev = 1
        
    # this is to use the model from day 1
    cfg.modelDir = f'{cfg.thisSubjectsDir}/model/'
    
    
    if cfg.dsAccessionNumber != "None":
        dicom_basedir = '/tmp/openneuro/'
        dicom_pattern = f'{dicom_basedir}/{cfg.dsAccessionNumber}/sub-{cfg.subjectNum}/func/'
    else:
        dicom_basedir = "/gpfs/milgram/project/realtime/DICOM/"
        dicom_pattern = f'{dicom_basedir}/{cfg.YYYYMMDD}.{cfg.rtFolder_subjectName}.{cfg.LASTNAME}*/'
    
    dns = glob.glob(dicom_pattern)
    if len(dns) == 0:
        dicom_dirname = dicom_pattern.replace("*","")
    else:
        dicom_dirname = dns[0]
    cfg.dicomDir = dicom_dirname
    cfg.maskOrig = f'{cfg.thisSubjectsDir}/reference/{cfg.mask}'
    cfg.maskToday = f'{cfg.thisSubjectsDir}/reference/ses_{cfg.session:02d}_mask.nii.gz'
    cfg.templateFuncVol=f'{cfg.thisSubjectsDir}/reference/templateFunctionalVolume_ses1.nii.gz'
    # make folder structure
    os.makedirs(cfg.thisSubjectsDir, exist_ok=True)
    os.makedirs(f'{cfg.thisSubjectsDir}/reference', exist_ok=True)
    for s in range(1,6):
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}', exist_ok=True)
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}/func', exist_ok=True)
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}/anat', exist_ok=True)
        os.makedirs(f'{cfg.thisSubjectsDir}/ses_{s:02d}/fmap', exist_ok=True)

    if verbose:
        print("In function configure_config_projectInterfaceOffline:")
        print(f'This subject dir: {cfg.thisSubjectsDir}')
        print(f'This session dir: {cfg.thisSubjectsDir}/ses_{cfg.session}:02d')
        print(f"This temp dir: {cfg.TempDir}")


    if os.path.exists(cfg.TempDir) and os.path.isdir(cfg.TempDir):
        shutil.rmtree(cfg.TempDir)
    os.makedirs(cfg.TempDir)
    if verbose: print("Loaded and returning config")
    if verbose: print(cfg.TempDir)
    return cfg


