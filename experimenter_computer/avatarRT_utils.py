import numpy as np
from scipy.stats import zscore
from MRAE.mrae import ManifoldRegularizedAutoencoder
from MRAE import dataHandler
from TPHATE.tphate import tphate
from sklearn.decomposition import PCA
import json, glob
import pandas as pd
import nibabel as nib
from nilearn.maskers import NiftiMasker
from sklearn.preprocessing import StandardScaler
from functools import reduce


project_path = '/gpfs/milgram/pi/turk-browne/users/elb77/BCI/rt-cloud/projects/avatarRT/'
data_path = '/gpfs/milgram/pi/turk-browne/users/elb77/BCI/rt-cloud/projects/avatarRT/experiment/subjects'

sub_numbers = np.arange(5, 26)
sub_numbers = sub_numbers[sub_numbers!=12]

def extract_data_from_df(dataframe, filter_columns, filter_values, statistic=None, return_index=False):
    ## takes a dataframe
    # filters columns to specific values
    # returns statistics
    indices = []
    for c,v in zip(filter_columns, filter_values):
        if type(v) == str:
            idx = list(dataframe.query(f'{c} == "{v}"').index)
        else:
            idx = list(dataframe.query(f"{c} == {v}").index)
        indices.append(idx)
    overlap_indices = reduce(np.intersect1d, indices)
    if statistic:
        return dataframe.iloc[overlap_indices][statistic].values
    if return_index: return dataframe.iloc[overlap_indices]
    return dataframe.iloc[overlap_indices].reset_index() 

# normalize function for standard implementation across scripts
# axis = 0 is assumed to be zscoring within voxel across time
def normalize(X, axis=0):
    X = zscore(X, axis=axis)
    return np.nan_to_num(X)

def format_subid(subject_number):
    try: sub=f'avatarRT_sub_{subject_number:02d}'
    except: sub=f'avatarRT_sub_{subject_number}'
    return sub

def load_behav_data_master_events(sub_ID, ses_ID, run=0):
    if run == 0:
        return pd.read_csv(f"{data_path}/{sub_ID}/{ses_ID}/behav/events_master.csv")
    return pd.read_csv(f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}_events_master.csv")
    

def reconcile_distance_labels_data(subject, session, run_num, embedding=False):
    labels = get_TR_labels_shifted(subject, session, run_num, labels=['x_norm_shifted','z_norm_shifted'])
    labels_x, labels_z, trs = labels['x_norm_shifted'], labels['z_norm_shifted'], labels['TRs_in_rounds']
    print(f'{len(labels_x)} | {len(labels_z)} | {len(trs)}')
    if embedding:
        data = load_masked_offline_embedding(subject, session, [run_num])[0]
    else:
        data = load_masked_offline_data(subject, session, [run_num])[0]
    
    trs = [int(t) for t in trs if t < data.shape[0]]
    print(f'data: {data.shape} | labels_x: {labels_x.shape} | trs: {len(trs)} | max: {np.max(trs)}')
    data_subset = data[trs,:]
    if data_subset.shape[0] < len(labels_x):
        labels_x=labels_x[:data_subset.shape[0]] 
        labels_z = labels_z[:data_subset.shape[0]] 
    
    return data_subset, labels_x, labels_z

def load_behav_data_TR_events(sub_ID, ses_ID, run=0):
    if run == 0:
        return pd.read_csv(f"{data_path}/{sub_ID}/{ses_ID}/behav/events_TRs.csv")
    return pd.read_csv(f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}_events_TRs.csv")

def get_TR_labels_shifted(sub_ID, ses_ID, run, labels=None):
    to_label = ['round_shifted', 'x_shifted', 'z_shifted','x_norm_shifted','z_norm_shifted'] if not labels else labels
    
    label_df = pd.read_csv(f'{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}_events_TRs.csv')
    TRs_to_include = np.load(f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}_round_TRs_0idx_label_shifted.npy")

    label_df = label_df[label_df['data_TRs-0idx'].isin(TRs_to_include)][to_label]
    to_return = {lab : label_df[lab].values for lab in to_label}
    to_return['TRs_in_rounds'] = TRs_to_include
    return to_return

def get_realtime_outdata(sub_ID, ses_ID, run, data_type=None):
    if data_type:
        return np.load(f"{data_path}/{sub_ID}/{ses_ID}/data/{sub_ID}_run_{run:02d}_{data_type}.npy")
    to_return = {}
    for data_type in ['masked_data','masked_raw_data','preproc_times','projected_data','analysis_times','decoded_angles','embedded_timeseries']:
        to_return[data_type] =  np.load(f"{data_path}/{sub_ID}/{ses_ID}/data/{sub_ID}_run_{run:02d}_{data_type}.npy")
    return to_return

def get_realtime_preproc_data(sub_ID, ses_ID, run, data_type=None):
    return np.load(f"{data_path}/{sub_ID}/{ses_ID}/data_rt_offline_preproc/{sub_ID}_run_{run:02d}_masked_data_v2.npy")
    

def get_subject_service_output(sub_ID, ses_ID, run):
    return pd.read_csv(f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/subject_service_output.csv")

def get_psychopy_output(sub_ID, ses_ID, run):
    fn=glob.glob(f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/psychopy_files/sub_{sub_ID}_run_*.csv")[0]
    
    return pd.read_csv(fn)
    
def load_masked_offline_data(sub_ID, ses_ID, runs):
    fns = [glob.glob(f'{data_path}/{sub_ID}/{ses_ID}/ROI_data/data/*run-{run:02d}*navigation*.npy')[0] for run in runs]
    data = [normalize(np.load(f)) for f in fns]
    return data

def load_masked_offline_embedding(sub_ID, ses_ID, runs):
    fns = [glob.glob(f'{data_path}/{sub_ID}/{ses_ID}/ROI_data/embeddings/*run-{run:02d}*navigation*.npy')[0] for run in runs]
    data = [np.load(f) for f in fns]
    return data

def embed_tphate(X, t=5, n_components=2):
    embd = tphate.TPHATE(t=t, n_components=n_components, verbose=0).fit_transform(X)
    return embd


# load in and initialize the MRAE model as specified by the config
def load_model_from_cfg(cfg):
    modelPath = cfg.modelDir
    perturbation = cfg.perturbation
    return load_model_from_dir(modelPath, perturbation=perturbation)

def load_model_from_dir(modelPath, perturbation=0, verbose=False):
    modelFilename = f'{modelPath}/state_dict.pt'
    modelSpec = f'{modelPath}/modelSpec.txt'
    bottleneck = np.load(f'{modelPath}/bottleneck.npy')
    
    if int(perturbation) == 0:
        if verbose: print("Loading intrinsic mapping")
        MANI_COMP = np.load(f'{modelPath}/manifold_pc_01.npy')
        TEST_RANGE = np.load(f'{modelPath}/test_range_01.npy') 
            
    elif int(perturbation) == 1:
        if verbose: print("Loading on-manifold perturbation")
        MANI_COMP = np.load(f'{modelPath}/manifold_pc_02.npy') 
        TEST_RANGE = np.load(f'{modelPath}/test_range_02.npy') 

    else:
        if verbose: print(f"Loading off-manifold perturbation; component={perturbation}")
        MANI_COMP = np.load(f'{modelPath}/manifold_pc_{perturbation:02d}.npy') 
        TEST_RANGE = np.load(f'{modelPath}/test_range_{perturbation:02d}.npy') 
    
    # this file contains the 1st and 99th percentile loadings of val data onto the pc of the AE bottleneck layer
    # get the slope and intercept for the mapping from manifold component to direction
    MAPPING_SLOPE, MAPPING_INTERCEPT = np.polyfit(TEST_RANGE, [-1, 1], 1)
    # set the mapping function
    with open(modelSpec, 'r') as f:
        modelParams = json.load(f)

    # initialize model
    this_mrae = ManifoldRegularizedAutoencoder(hidden_dim=int(modelParams['hidden_dim']),
                                                    manifold_dim=int(modelParams['manifold_dim']),
                                                    IO_dim=int(modelParams['IO_dim']))
    this_mrae.load_model_state_dict(modelFilename) # load in the pretrained weights
    this_mrae.manifold_regularization = bottleneck # pass bottleneck
    return this_mrae, MAPPING_SLOPE, MAPPING_INTERCEPT, MANI_COMP

def load_neurofeedback_component(modelPath, perturbation):
    if int(perturbation) == 0:
        MANI_COMP = np.load(f'{modelPath}/manifold_pc_01.npy')            
    elif int(perturbation) == 1:
        MANI_COMP = np.load(f'{modelPath}/manifold_pc_02.npy') 
    else:
        MANI_COMP = np.load(f'{modelPath}/manifold_pc_{perturbation:02d}.npy') 
    return MANI_COMP

# define the component of the manifold
def get_manifold_component(manifold, n_components=None):
    pca = PCA(n_components=n_components).fit(manifold)
    return pca.components_

# project a new array x into the fitted MRAE's manifold layer and extract the projection
def project_new_data(mrae_model, x):
    xDataset = dataHandler.TestDataset(x)
    xProj = mrae_model.extract_projection_to_manifold(xDataset)
    return xProj


# map projected data onto the fitted PC
def map_projection(projected_data, fitted_component):
    loading = np.dot(projected_data, fitted_component.T)
    return loading

def get_target_location(sub_ID, ses_ID, run, trial):
    fn = f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/round_{trial:02d}/gameboard.txt"
    board = pd.read_csv(fn)
    tar = board[board["ObjectType"]=="Target"]
    tar.x = tar.x.astype(float)
    tar.z = tar.z.astype(float)
    x, z = tar.x.values.mean(), tar.z.values.mean()
    return x, z

def get_start_location_unity(sub_ID, ses_ID, run, trial):
    fn = f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/round_{trial:02d}/player_transform.txt"
    transf = pd.read_csv(fn)
    transf = transf[transf["Event"]=="Transform.Position"]
    x, z = transf.x.values[0], transf.z.values[0]
    return x,z

def get_position_file(sub_ID, ses_ID, run, trial):
    fn = f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/round_{trial:02d}/player_transform.txt"
    transf = pd.read_csv(fn)
    transf = transf[transf["Event"]=="Transform.Position"]
    return transf

def get_end_location_unity(sub_ID, ses_ID, run, trial):
    fn = f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/round_{trial:02d}/player_transform.txt"
    transf = pd.read_csv(fn)
    transf = transf[transf["Event"]=="Transform.Position"]
    x, z = transf.x.values[-1], transf.z.values[-1]
    return x,z

def get_distance_traveled(sub_ID, ses_ID, run, trial, threshold=0.008):
    fn = f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/round_{trial:02d}/player_transform.txt"
    transf = pd.read_csv(fn)
    transf = transf[transf["Event"]=="Transform.Position"]
    x_vec, z_vec = transf.x.values, transf.z.values
    delta = 0 
    for i in range(1,len(x_vec)):
        x0, z0 = float(x_vec[i-1]), float(z_vec[i-1])
        x1, z1 = float(x_vec[i]), float(z_vec[i])
        dist = np.sqrt((x0-x1)**2 + (z0-z1)**2)
        if dist > threshold: delta+=dist
    return delta

def get_trial_error(sub_ID, ses_ID, run, trial):
    delta = get_distance_traveled(sub_ID, ses_ID, run, trial)
    pathlen = get_shortest_path_length(sub_ID, ses_ID, run, trial)
    return (delta-pathlen)/pathlen

def get_shortest_path_length(sub_ID, ses_ID, run, trial):
    fn = f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/round_{trial:02d}/player_transform.txt"
    transf = pd.read_csv(fn)
    transf = transf[transf["Event"]=="Transform.Position"]
    x0, z0 = float(transf.x.values[0]), float(transf.z.values[0] )
    x1, z1 = float(transf.x.values[-1]), float(transf.z.values[-1])
    pathlen = np.sqrt((x0-x1)**2 + (z0-z1)**2)
    return pathlen

def get_n_trials(sub_ID, ses_ID, run):
    dns = glob.glob(f"{data_path}/{sub_ID}/{ses_ID}/behav/run_{run:03d}/round_*")
    n = [int(f.split('_')[-1]) for f in dns]
    return np.max(n)
                  
def get_perturbation_info(sub_ID, ses_ID, run, return_component=False):
    if run == 1: 
        if return_component: return 'IM', 0
        else: return 'IM'
    
    df = pd.read_csv(f'{project_path}/offline_analyses/info/session_tracker.csv')
    sub_num = str(int(sub_ID.split('_')[-1]))
    ses_num = int(ses_ID.split('_')[-1])
    row = df[df['subject_number'] == sub_num]
    if row.omp_session.item() == ses_num: 
        perturb_type = "OMP"
    elif row.im_session.item() == ses_num:
        perturb_type = "IM"
    else: 
        perturb_type = "WMP"
    if not return_component:  return perturb_type
    if perturb_type == 'OMP': comp = int(row.omp_component.item())
    elif perturb_type == 'WMP': comp = int(row.wmp_component.item())
    else: comp = 0
    return perturb_type, comp

def load_ts_regressor_file(sub_ID, ses_ID, run_ID, col_name=None):
    fn = f'{data_path}/{sub_ID}/regressors/{sub_ID}_{ses_ID}_run_{run_ID:02d}_timeseries_regressors.csv'
    df = pd.read_csv(fn,index_col=0)
    if col_name: return df[col_name]
    return df

def load_vol_data(sub_ID, ses_ID, run_ID, space='standard', asarray=False, normalize=True):
    task = 'RT'
    root = f'{data_path}/{sub_ID}/{ses_ID}/func/{sub_ID}_task-{task}_run-{run_ID:02d}'
    if ses_ID == 'ses_01': task='joystick'
    if space == 'standard': tail = '_bold_preproc_v2_MNI152_2mm.nii.gz'
    elif space == 'native': tail = '_bold_preproc_v2_native.nii.gz'
    else: tail = '_bold.nii'
    fn = f'{root}{tail}'
    X = nib.load(fn)
    if asarray: return X.get_fdata()
    return X

def mask_normalize_standard_vol_data(nii, normalize=False, asarray=False):
    mask = f'/gpfs/milgram/apps/hpc.rhel7/software/FSL/6.0.5-centos7_64/data/standard/MNI152_T1_2mm_brain_mask.nii.gz'
    mask=nib.load(mask)
    nifti_masker = NiftiMasker(mask_img=mask,standardize=normalize)
    maskedData = nifti_masker.fit_transform(nii)
    maskedNii = nifti_masker.inverse_transform(maskedData)
    return maskedNii

def mask_scaler_standard_vol_data(nii, feature_range=(-1,1), asarray=False):
    mask = f'/gpfs/milgram/apps/hpc.rhel7/software/FSL/6.0.5-centos7_64/data/standard/MNI152_T1_2mm_brain_mask.nii.gz'
    mask=nib.load(mask)
    nifti_masker = NiftiMasker(mask_img=mask,standardize=normalize)
    maskedData = nifti_masker.fit_transform(nii)
    maskedNii = nifti_masker.inverse_transform(maskedData)
    return maskedNii
    

def shift_timing(label_TR, n_TRs, TR_shift_size,shift_val=0):
    
    # Create a short vector of extra labels
    shift_label = np.ones((TR_shift_size,))*shift_val

    # Zero pad the column from the top.
    label_TR_shifted = np.concatenate((shift_label, label_TR))

    # Don't include the last rows that have been shifted out of the time line.
    label_TR_shifted = label_TR_shifted[:n_TRs]
    
    return label_TR_shifted