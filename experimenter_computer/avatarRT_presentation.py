## This script handles stimulus presentation for the rt avatar project. This does NOT do any communication with the
## real-time cloud - that is all handled by a parallel script, 'run_subject_service.py'.
## This script handles triggers from the scanner, communication about game state with Unity, and all staircasing,
## as well as experimental timing -- all written out to file.


import pandas as pd
import psychopy, os, socket, sys, glob
from psychopy import visual, event, core, logging, gui, data, monitors, sound
from psychopy.hardware.emulator import launchScan, SyncGenerator
import matplotlib.pyplot
from psychtoolbox import PsychHID, GetSecs, hid
from configure_config import configure_config_subjectPresentation, game_mode_mapping
import logging, argparse, threading
from presentation_utils import write_paramfile, setup  # , getTriggers, getKeys
from psychopy.data import QuestHandler, StairHandler
import numpy as np

if 'watts' in os.getcwd():
    main_dir = "/home/watts/Desktop/ntblab/erica/rt-cloud/projects/avatarRT/"
    ThisComputer = 'Linux'
elif 'elb' in os.getcwd():
    main_dir = "/Users/elb/Desktop/Unity_Games/BCI_experiment/rt-cloud/projects/avatarRT/"
    ThisComputer = 'Laptop'
else:
    main_dir = "/Users/ericabusch/Desktop/BCI/rt-cloud/projects/avatarRT/"
    ThisComputer = 'Tars'
sys.path.append(main_dir)
sys.path.append(main_dir + '../../')

currPath = os.path.dirname(os.path.realpath(__file__))
rootPath = os.path.dirname(currPath)
sys.path.append(rootPath)

# set backend
matplotlib.pyplot.switch_backend("Agg")
if "watts" in os.getcwd():
    import ctypes

    xlib = ctypes.cdll.LoadLibrary("libX11.so")
    xlib.XInitThreads()

################### Set up a bunch of global variables ############################
TRIGGER_COUNTER = 0
CONNECTED = False
UnityEnded = False
QPRESS = False
RPRESS = False
incomingMessageCounter, outgoingMessageCounter = 0, 0
timeout_time = 500
threads = []
OUT_DF = pd.DataFrame(columns=["GlobalTime", "Event", "EventValue", "TriggerCount"])
COMM_DF = pd.DataFrame(columns=["GlobalTime", "Event", "EventValue", "TriggerCount"])
prevOutgoingMessage = ""
DF_OUTNAME = ""
prompt = False
joy_file = 0
verbose = True
Successes = []
GameControls = []
intensity_step = 0.04
ThisLevel = 1

#######################################################################################
######### Housekeeping functions ####################
def savedata(DF_OUTNAME):
    '''
    Takes the filename that we want to save all scanner communication files with,
    sorts them according to TR value, and outputs them to DF_OUTNAME.

    If the game is in real-time mode, it also outputs all of the quest staircasing information
    to npy files too.
    '''

    global COMM_DF, OUT_DF
    # handle the dataframes
    OUT_DF = pd.concat([COMM_DF, OUT_DF]).sort_values(by='TriggerCount').reset_index(drop=True)

    # take out duplicate messages
    OUT_DF.drop_duplicates(subset=["EventValue"], keep=False, inplace=True)

    OUT_DF.to_csv(DF_OUTNAME)
    if cfg.GameMode == 'ScannerMove':
        # save the quest parameters for this
        np.save(f"{cfg.run_outdir}/staircase_results.npy", Successes)
        np.save(f"{cfg.run_outdir}/staircase_intensities.npy", GameControls)
        print(f"Saved staircase to: {cfg.run_outdir}")

    if verbose: print(f"Wrote to {DF_OUTNAME}")
    #os.remove(f"psychopy_checkpoint_run_{p.run:02d}.csv")
    if verbose: print("Deleted checkpoint.")
    # clear comm dir
    comm_fns = glob.glob(f'{cfg.run_outdir}/scanner_comms/scanner_output*')
    for f in comm_fns:
        os.remove(f)
    if verbose: print(f'deleted {len(comm_fns)} comm files')



def checkpoint_data():
    global COMM_DF, OUT_DF, TRIGGER_COUNTER
    # handle the dataframes
    x = COMM_DF.copy()
    y = OUT_DF.copy()
    TEMP_DF = pd.concat([x, y])

    # take out duplicate messages
    TEMP_DF.drop_duplicates(subset=["EventValue"], keep=False, inplace=True)

    TEMP_DF.to_csv(f"{p.psychopy_dir}/psychopy_checkpoint_run_{p.run:02d}.csv")
    print("Checkpointed psychopy file")


############# Communication functions with unity ###############################
def recv():
    '''
    While unity is still connected, try to read data from the socket, and save
    it as the incoming data
    '''

    global incomingMessage, sock, UnityEnded, incomingMessageCounter
    while not UnityEnded:
        sockData = sock.recv(1024).decode("UTF-8")
        if sockData:
            print(f"Received: {sockData}")
            incomingMessage = sockData
            incomingMessageCounter += 1
    return


def send():
    '''
    While unity is still connected, if there's a message to be sent to unity, send it and keep track of it.
    '''
    global outgoingMessage, prevOutgoingMessage, sock, UnityEnded, outgoingMessageCounter

    while not UnityEnded:
        if outgoingMessage is not None and len(outgoingMessage) > 1:
            if prevOutgoingMessage != outgoingMessage:
                sock.sendall(outgoingMessage.encode("UTF-8"))
                prevOutgoingMessage = outgoingMessage
                print(f"Sent: {outgoingMessage}")
                if "Calibration" not in outgoingMessage:
                    outgoingMessageCounter += 1
                outgoingMessage = None
    return


def run_staircase(response, current_intensity=None, window=2):
    """
    at the end of each round, take the success value of the staircase
    and input that to the pre-initialized staircase
    and run the staircase to get the next output value.
    """
    if current_intensity == None:
        with open(f'{cfg.run_outdir}/scanner_comms/staircase_value.txt', 'r') as f:
            current_intensity = float(f.read())
    GameControls.append(current_intensity)
    Successes.append(response)
    next_intensity = current_intensity + intensity_step * response # response is -1 if decreasing intensity, 1 if increasing

    with open(f'{cfg.run_outdir}/scanner_comms/staircase_value.txt', 'w') as f:
        f.write(str(next_intensity))
    if verbose: print(
        f"Previous intensity: {np.round(current_intensity, 4)} | DeltaControl: {response*intensity_step} | new intensity: {np.round(next_intensity, 4)}")


def initialize_staircase():
    global ThisLevel
    """
    initialize the quest staircase with the values from the config file (these may need to be rethought)
    """
    # load in staircasing from previous run
    prev_dir = f'{cfg.thisSubjectsDir}/ses_{cfg.session:02d}/behav/run_{p.run - 1:03d}/'
    print(f"Initializing staircase from {prev_dir}")
    prev_fn = f'{prev_dir}/scanner_comms/staircase_value.txt'
    if os.path.exists(prev_fn):
        try:
            with open(prev_fn,'r') as f:
                line=f.readline()
            val = float(line)
            current_intensity = val
            print(f"Initialized staircase from {prev_dir} as {val}") # CHECK IF THERE IS A BUG EHRE WITH READING IN THE FILE
        except:
            val = cfg.GameControlStaircase
            print(f"Initialized staircase from cfg as {val}")
    else:
        val = cfg.GameControlStaircase
        print(f"Initialized staircase from cfg as {val}")

    with open(f'{cfg.run_outdir}/scanner_comms/staircase_value.txt', 'w') as f:
        f.write(str(val))
        print(f'Wrote {val} to {cfg.run_outdir}/scanner_comms/staircase_value.txt')

    # if we're on the first run, load the level from the cfg
    # if p.run == 1:
    #     ThisLevel = cfg.StartLevel
    #     if ThisLevel =
    # else:
    # initialize level based on staircasing:
    if val >= cfg.StartStaircase:
        ThisLevel = 1
    else:
        ThisLevel = (cfg.StartStaircase - val) / intensity_step + 1 # without the plus, would be 0 indexed

    ThisLevel=int(ThisLevel)
    with open(f'{cfg.run_outdir}/scanner_comms/display_level.txt', 'w') as f:
        f.write(str(ThisLevel))
        print(f'Wrote {ThisLevel} to {cfg.run_outdir}/scanner_comms/display_level.txt')


def run_unity_client():
    """
    Runs all communication with the unity client.
    This creates a TCP connection via python and unity and allows messages to be passed between them.

    """

    global QPRESS, TRIGGER_COUNTER, incomingMessageCounter, outgoingMessageCounter, \
        incomingMessage, sock, outgoingMessage, CONNECTED, UnityEnded, threads, COMM_DF, calibrationPause

    COMM_DF = pd.DataFrame(columns=['GlobalTime', 'Event', 'EventValue', 'TriggerCount'])
    core.wait(1)
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(timeout_time)

    # Check and turn on TCP Keep alive
    x = sock.getsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE)
    if x == 0:
        if verbose:
            print("Socket Keepalive is off; turning on.")
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
    elif verbose:
        print("Socket Keepalive already on.")

    incomingMessage, outgoingMessage, messageCounter = None, None, 0

    # start the socket
    print(f'Connecting to {cfg.UNITY_HOST}:{cfg.UNITY_PORT}')
    sock.connect((cfg.UNITY_HOST, cfg.UNITY_PORT))
    sock_receiver = threading.Thread(target=recv)
    sock_receiver.daemon = True
    sock_receiver.start()
    sock_sender = threading.Thread(target=send)
    sock_sender.daemon = True
    sock_sender.start()
    threads.append(sock_receiver)
    threads.append(sock_sender)

    while True:
        CONNECTED = True

        if outgoingMessageCounter == 0:
            if not calibrationPause:
                outgoingMessage = f"Connected_TR_{TRIGGER_COUNTER}"
            else:
                outgoingMessage = f"Calibration_{TRIGGER_COUNTER}"
            COMM_DF.loc[len(COMM_DF)] = {"GlobalTime": str(globalClock.getTime()),
                                         'Event': "OutgoingMessage",
                                         'EventValue': outgoingMessage,
                                         'TriggerCount': TRIGGER_COUNTER}

        elif outgoingMessageCounter == 1:
            if outgoingMessage is None and not calibrationPause:
                outgoingMessage = f'Start_TR_{TRIGGER_COUNTER}'
                COMM_DF.loc[len(COMM_DF)] = {"GlobalTime": str(globalClock.getTime()),
                                             'Event': "OutgoingMessage",
                                             'EventValue': outgoingMessage,
                                             'TriggerCount': TRIGGER_COUNTER}

        if incomingMessage is not None:
            COMM_DF.loc[len(COMM_DF)] = {"GlobalTime": str(globalClock.getTime()),
                                         'Event': "IncomingMessage",
                                         'EventValue': incomingMessage,
                                         'TriggerCount': TRIGGER_COUNTER}
            if "End" in incomingMessage:
                outgoingMessage = f"GotEnd_TR_{TRIGGER_COUNTER}"

                if cfg.GameMode == "ScannerMove":
                    s = int(incomingMessage.split("_")[-1])
                    print(f"parsed s = {s}")
                    run_staircase(s)

            elif "Begin" in incomingMessage:
                outgoingMessage = f"GotBegin_TR_{TRIGGER_COUNTER}"

            elif "ReceivedCalib_9" in incomingMessage:
                # outgoingMessageCounter += 1
                outgoingMessage = f'Start_TR_{TRIGGER_COUNTER}'

            core.wait(0.1)
            checkpoint_data()  # checkpoint the data just in case
            incomingMessage = None

        elif QPRESS:
            outgoingMessage = "Quitting"
            COMM_DF.loc[len(COMM_DF)] = {"GlobalTime": str(globalClock.getTime()),
                                         'Event': "OutgoingMessage",
                                         'EventValue': outgoingMessage,
                                         'TriggerCount': TRIGGER_COUNTER}
            break

    # Once it's over, close & clean up
    CONNECTED = False
    UnityEnded = True
    sock_sender.join()
    sock.close()
    return


def getTriggers(joystickIndex, keyboardIndex):
    """
    Checks for triggers coming in from the joystick and/or the keyboard simultaneously
    """

    gottenTrigs, gottenKeys = [], []
    while PsychHID('KbQueueFlush', [joystickIndex]):
        evt = PsychHID('KbQueueGetEvent', [joystickIndex])[0]
        evt2 = PsychHID('KbQueueGetEvent', [keyboardIndex])[0]
        if evt and evt['Pressed']:
            K = chr(int(evt['CookedKey'])).lower()
            gottenTrigs.append((K, evt['Time']))
        if evt2 and evt2['Pressed']:
            K = chr(int(evt2['CookedKey'])).lower()
            gottenKeys.append((K, evt2['Time']))
    return gottenTrigs, gottenKeys


def getKeys(keyboardIndex):
    '''
    Checks for input coming in from the keyboard only (this is only useful for debugging)
    '''
    gottenKeys = []
    while PsychHID('KbQueueFlush', [keyboardIndex]):
        evt = PsychHID('KbQueueGetEvent', [keyboardIndex])[0]
        if evt['Pressed']:
            K = chr(int(evt['CookedKey'])).lower()
            gottenKeys.append((K, evt['Time']))
    return gottenKeys


def run_psychopy(scanner=True):
    global QPRESS, TRIGGER_COUNTER, CONNECTED, UnityEnded, threads, OUT_DF, outgoingMessage, calibrationPause
    MR_settings, mywin, scanmode, DF_OUTNAME = setup(cfg, p)

    # set up keyboard and joystick (or button box whatever) trigger collection
    keyboardIndices, keyboardNames, _ = hid.get_keyboard_indices()
    keybuff = [1] * 256
    keyboardIndex = keyboardIndices[keyboardNames.index(cfg.KEYBOARD_NAME)]
    # print(keyboardIndex, cfg.KEYBOARD_NAME)
    PsychHID('KbQueueCreate', [keyboardIndex], keybuff)
    PsychHID('KbQueueStart', [keyboardIndex])

    if scanner:
        joystickIndex = keyboardIndices[keyboardNames.index(cfg.JOYSTICK_NAME)]
    else:
        joystickIndex = keyboardIndices[keyboardNames.index(cfg.KEYBOARD_NAME)]
    PsychHID('KbQueueCreate', [joystickIndex], keybuff)
    PsychHID('KbQueueStart', [joystickIndex])

    _ = GetSecs()
    event.clearEvents()
    input("************ NOW YOU CAN START UNITY. PRESS ENTER TO CONFIRM. ************")
    unity_thread = threading.Thread(target=run_unity_client)
    unity_thread.daemon = True

    vol = launchScan(mywin,
                     MR_settings,
                     globalClock=globalClock,
                     simResponses=None,
                     mode=scanmode,
                     esc_key='escape',
                     instr=None,
                     wait_msg='Wait for start!',
                     wait_timeout=200,
                     log=True)

    if verbose: print("Pausing for first scanner trigger.")
    while TRIGGER_COUNTER == 0:
        kb_triggers, kb_presses = getTriggers(joystickIndex, keyboardIndex)
        while not kb_triggers:
            mywin.flip()
            kb_triggers, kb_presses = getTriggers(joystickIndex, keyboardIndex)
        triggers, trigger_time = kb_triggers[0]
        if scanner:
            keys, key_times = kb_presses[0]
        else:
            keys, key_times = kb_triggers[0]
        if '5' in triggers or '5' in keys:
            TRIGGER_COUNTER += 1
            print(f"TR={TRIGGER_COUNTER}")

    if (cfg.game_mode == 3) and (TRIGGER_COUNTER <= cfg.Calibration_TRs):
        calibrationPause = True
    else:
        calibrationPause = False

    unity_thread.start()
    threads.append(unity_thread)
    if verbose: print("Launched unity thread.")

    # Keep looping here unless we get a Q press
    while TRIGGER_COUNTER <= cfg.num_total_TRs + cfg.end_buffer:
        event.clearEvents()                

        # keep looking for triggers from these two places
        kb_triggers, kb_presses = getTriggers(joystickIndex, keyboardIndex)
        while not kb_triggers:
            mywin.flip()
            kb_triggers, kb_presses = getTriggers(joystickIndex, keyboardIndex)
        keys, key_time = kb_presses[0] if kb_presses else [], 0
        triggers, trig_time = kb_triggers[0] if kb_triggers else [], 0

        if '5' in triggers or 't' in keys:
            TRIGGER_COUNTER += 1
            if not CONNECTED:
                print(f"Waiting for unity to connect; TR = {TRIGGER_COUNTER}")
            else:
                print(f"TR={TRIGGER_COUNTER}")
                # if we're in scanner mode, make sure to send the appropriate pause messages
                if (cfg.game_mode == 3) and (TRIGGER_COUNTER <= cfg.Calibration_TRs):
                    calibrationPause = True
                    outgoingMessage = f"Calibration_{TRIGGER_COUNTER}"
                else:
                    # print("Making calib false")
                    calibrationPause = False

            OUT_DF.loc[len(OUT_DF)] = {"GlobalTime": str(globalClock.getTime()),
                                       "Event": "Trigger",
                                       "EventValue": TRIGGER_COUNTER,
                                       'TriggerCount': TRIGGER_COUNTER}
        if "q" in keys or "q" in triggers:
            QPRESS = True
            print(f"Caught q on TR={TRIGGER_COUNTER}")
            OUT_DF.loc[len(OUT_DF)] = {"GlobalTime": str(globalClock.getTime()),
                                       "Event": "QPress",
                                       "EventValue": 'Q',
                                       'TriggerCount': TRIGGER_COUNTER}
            event.clearEvents()
            break

    PsychHID('KbQueueStop', keyboardIndex)
    PsychHID('KbQueueStop', joystickIndex)
    mywin.close()
    savedata(DF_OUTNAME)
    return


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config', '-c', type=str,
                        help='configuation file for subject presentation')
    parser.add_argument('--run', '-r', action='store', default=None, type=int, required=True)
    parser.add_argument('--debugging_mode', '-b', type=int, default=1, action='store', help='Debugging mode?')
    parser.add_argument('--using_psychopy', '-pp', type=bool, default=True, help='start scan simulation?')
    parser.add_argument('--verbose', '-v', type=bool, default=True)
    parser.add_argument('--game_mode', '-g', type=int, default=None)
    p = parser.parse_args()
    verbose = p.verbose
    p.prompt = prompt
    print("**************** PAUSE BEFORE STARTING UNITY ****************")
    cfg = configure_config_subjectPresentation(p.config, p.run, verbose)

    if p.game_mode == None:
        p.game_mode = cfg.game_mode
    else:
        cfg.game_mode = p.game_mode
        cfg.GameMode = game_mode_mapping[p.game_mode]

    print(f"Beginning subject: {cfg.LASTNAME} game mode: {cfg.GameMode} run:{p.run}")
    if cfg.GameMode == "ScannerMove":
        if verbose and cfg.run == 1: print(f"Starting staircase at {cfg.GameControlStaircase}")
        initialize_staircase()

    basedir = cfg.DEFAULT_EXPERIMENT_DIR
    p.experiment_dir = cfg.run_outdir
    # if verbose: print(f"experiment dir: {p.experiment_dir}")
    paramfn = f"{basedir}/{cfg.UNITY_PARAM_FN}"
    write_paramfile(paramfn, p.run, cfg)
    globalClock = core.Clock()

    if 'watts' in os.getcwd() and p.debugging_mode == 0:
        run_psychopy(scanner=True)
    else:
        run_psychopy(scanner=False)
    core.quit()
