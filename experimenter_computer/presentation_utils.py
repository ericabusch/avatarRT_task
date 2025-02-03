import os, sys, shutil
from psychopy import visual, event, core, logging, gui, data, monitors, sound
from psychtoolbox import PsychHID, GetSecs, hid


def write_paramfile(paramfn, run, cfg):
    if cfg.difficulty == 0:
        cfg.difficulty = run

    with open(paramfn, 'w') as f:
        f.write(f'{cfg.subjectName}\n')
        f.write(f'{run}\n')
        f.write(f'{cfg.scanNum[0]}\n')
        f.write(f'{cfg.run_outdir}\n')
        f.write(f'{cfg.GameMode}\n')
        f.write(f'{cfg.difficulty}\n')
        f.write(f'1\n')
        f.write(f'{cfg.noisy_level}\n')
        if cfg.GameMode == 'ScannerMove':
            f.write(f'{cfg.StartLevel}\n')
    #if cfg.verbose: print(f"Wrote to {paramfn}")

def setup(cfg, p):
    if p.debugging_mode == 1:
        monitor_name = 'testMonitor'
        scanmode = 'Test'
    else:
        monitor_name = 'scanner'
        scanmode = 'Scan'


    mywin = visual.Window(cfg.win_size, pos=cfg.win_pos, monitor=monitor_name, units='pix',
                          fullscr=False, waitBlanking=False, allowGUI=False)
    MR_settings = {"TR": cfg.TR, "volumes": cfg.num_total_TRs, "sync": 5, "skip": cfg.TR_discard, "sound": False}
    os.makedirs(cfg.run_outdir, exist_ok=True)
    os.makedirs(cfg.run_outdir+'/scannerComms', exist_ok=True)
    os.makedirs(cfg.run_outdir + '/scanner_comms', exist_ok=True)
    if p.verbose: print(f"Made {cfg.run_outdir}")
    p.psychopy_dir = f'{cfg.run_outdir}/psychopy_files'
    os.makedirs(p.psychopy_dir, exist_ok=True)
    DF_OUTNAME = f'{p.experiment_dir}/psychopy_files/sub_{cfg.subjectName}_run_{p.run:03d}_events.csv'
    return MR_settings, mywin, scanmode, DF_OUTNAME

# Function that polls for keys from the joystick and the keyboard simultaneously
# Joystick could also refer to button box -- whatever is sending the scanner trigger over
def getTriggers(joystickIndex, keyboardIndex):
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
    gottenKeys = []
    while PsychHID('KbQueueFlush', [keyboardIndex]):
        evt = PsychHID('KbQueueGetEvent', [keyboardIndex])[0]
        if evt['Pressed']:
            K = chr(int(evt['CookedKey'])).lower()
            gottenKeys.append((K, evt['Time']))
    return gottenKeys
