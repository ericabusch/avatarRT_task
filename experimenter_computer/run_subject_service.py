## This script is run to catch the output of the project interface, running on Milgram
## via the SSL connection. Each TR, the project interface sends one value (the direction)
## and this script also finds a "game control" parameter file (output by the staircasing procedure
## in the presentation script), and it writes out those two values to a file (adjusted by the number of calibration TRs)
## so that unity can read in those files and use them to update movement.
## It also saves all of this information to a final CSV file, separately for each run.

## This script requires 3 command-line arguments: the config file for this session, the run number, and the server information
# Example run from laptop:

# python3 run_subject_service.py -c conf/avatarRT_sub_01_ses_01.toml -r 1 -s localhost:6664


import pandas as pd
from configure_config import configure_config_subjectPresentation
import os
import sys, shutil
import argparse
import numpy as np
from subprocess import call

# Add paths
if 'watts' in os.getcwd():
    main_dir = "/home/watts/Desktop/ntblab/erica/rt-cloud/projects/avatarRT/"
else:
    main_dir="/Users/elb/Desktop/BCI/rt-cloud/projects/avatarRT/"

sys.path.append(main_dir)
sys.path.append(main_dir+'../../')

currPath = os.path.dirname(os.path.realpath(__file__))
rootPath = os.path.dirname(currPath)
sys.path.append(rootPath)
from rtCommon.subjectService import SubjectService
from rtCommon.wsRemoteService import WsRemoteService, parseConnectionArgs

# parse connection args
# These include: "-s <server>", "-u <username>", "-p <password>", "--test",
#   "-i <retry-connection-interval>"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--config','-c',type=str,
                    help='configuration file for subject presentation')
parser.add_argument('--run', '-r', type=int, default=1)
parser.add_argument('--server','-s',action='store',
                    dest='server', required=False, default=None)
parser.add_argument('-i', '--interval', action='store', dest='interval', type=int, default=5)
parser.add_argument('-u', '--username', action='store', dest='username', default='elb77')
parser.add_argument('-p', '--password', action='store', dest='password', default='avatar4')
parser.add_argument('--subjectRemote' ,type=bool, default=False)

p = parser.parse_args()
cfg = configure_config_subjectPresentation(p.config, p.run)

# set up connection
connectionArgs = parseConnectionArgs()
connectionArgs.server = cfg.server

args, _ = parser.parse_known_args(namespace=connectionArgs)
comm_output = f'{cfg.run_outdir}/scanner_comms/'

#if os.path.exists(comm_output):
#    shutil.rmtree(comm_output)
#os.makedirs(comm_output)

print(f"Starting subject service for run {p.run} | scanner output to {comm_output}")

subjService = SubjectService(connectionArgs)
subjService.runDetached()
timestamp_prev =0

df = pd.DataFrame(columns=['MilgramTR','Run','MilgramTimestamp','Value','Confidence'])

# if not os.path.exists(f'{comm_output}/staircase_value.txt'):
#     with open(f'{comm_output}/staircase_value.txt', 'w') as f:
#         f.write(str(cfg.GameControlStaircase))

trId = 0

while True and trId < cfg.num_total_TRs :
    try:
        feedbackMsg = subjService.subjectInterface.msgQueue.get(block=True, timeout=300)
    except:
        break
    trId = feedbackMsg.get('trId', None)
    value = feedbackMsg.get('value', None)
    timestamp = feedbackMsg.get('timestamp', None)
    print(f'Timestamp: {np.round(timestamp-timestamp_prev,4)}')
    # this is output from the staircasing
    with open(f'{comm_output}/staircase_value.txt', 'r') as f:
        try:
            game_control = float(f.read())
        except:
            print(f'Could not read as float: {f.read()}')
            game_control = 0.5

    with open(f'{comm_output}/scanner_output_{trId-cfg.Calibration_TRs}.txt', 'w') as f:
        f.write(f'{value},{game_control}')
        # print(f'Incoming data: scannerTR={trId}, value={value}, time={timestamp - timestamp_prev}')
    df.loc[len(df)] = {'MilgramTR': trId,
                       'Run': feedbackMsg.get("run",None),
                       "MilgramTimestamp":timestamp,
                       "Value":value,
                       "Confidence":game_control}
    timestamp_prev = timestamp
df.to_csv(cfg.run_outdir+'/subject_service_output.csv')
command = f'rm -f {cfg.run_outdir}/scanner_comms/scanner_output*.txt'
call(command, shell=True)
print(f'After clearing: ', os.listdir(f"{cfg.run_outdir}/scanner_comms/"))
print(f'{cfg.run_outdir}/subject_service_output.csv')
