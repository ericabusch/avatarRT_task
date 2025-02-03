import os,  glob, time
import numpy as np
import sys

# in params2.txt, change line 7 to 0 (to allow it to work without waiting for python)

# figure out where to output stuff
path ='/Users/elb/Desktop/BCI/avatarRT_task/experiment/subjects/avatarRT_sub_ABC/ses_02/run_001'
os.makedirs(path+'/scanner_comms/', exist_ok=True)
TR=2
print(path)

def clear_dir():
    fns = glob.glob(path+'/scanner_comms/*')
    for f in fns:
        os.remove(f)
    print('cleared dir')


def simulate_data(angle, conf, TR):
    clear_dir()
    for i in range(len(angle)):
        with open(path+f'/scanner_comms/scanner_output_{i}.txt', 'w') as f:
            f.write(f'{angle[i]},{conf[i]}')
        time.sleep(TR)
        if i % 10 == 0:
            print(i)

n_timepoints = int(sys.argv[1])
noisy_angles = np.random.randint(-1 * n_timepoints, n_timepoints, n_timepoints) / float(n_timepoints)
z = np.zeros_like(noisy_angles)
one = np.ones_like(noisy_angles)
decreas_conf_half = np.linspace(.999, 0.5, n_timepoints)
decreas_conf = np.linspace(1, 0, n_timepoints)[::-1]
simulate_data(noisy_angles, one*0.25, TR)