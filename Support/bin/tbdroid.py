#!/bin/env python

# Copyright 2020 The Tilt Brush Authors
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# A tool for doing various operations on Tilt Brush when its running on an android device.
import subprocess
import os
import os.path
import sys
import signal
import tempfile
import re
import glob
import time

FNULL = open(os.devnull, 'w')
printInfo = True;
tbdir = "/storage/emulated/0/Android/data/com.google.tiltbrush/files/"
tiltbrushcfg = tbdir + """Tilt\ Brush/Tilt\ Brush.cfg"""
tiltbrushcfgunescaped = tbdir + "Tilt Brush/Tilt Brush.cfg"
sketchpath = tbdir + "Tilt Brush/Sketches"
configcopy=None
defaulttemplate = os.path.realpath(os.path.join(
    os.path.dirname(os.path.realpath(__file__)), "../TiltBrushCfgTemplate.cfg"))
screenshotnum = 1

def run():
  info ("Running Tilt Brush...")
  #subprocess.check_call(
  #  "adb shell monkey -p com.google.tiltbrush -c android.intent.category.LAUNCHER 1", stdout=FNULL,
  #  stderr=FNULL)
  subprocess.check_call(
      "adb shell am start com.google.tiltbrush/com.unity3d.player.UnityPlayerActivity",
      stdout=FNULL, stderr=FNULL)
  info("Done.")

def kill():
  info("Killing any existing tilt Brush...")
  subprocess.check_call("adb shell am force-stop com.google.tiltbrush", stdout=FNULL)
  info("Done.")

def get_profile_data():
  subprocess.check_call("adb logcat -c")
  proc = subprocess.Popen("adb logcat -v raw", stdout=subprocess.PIPE)
  started = False
  finished = False
  while(not finished):
    line = proc.stdout.readline()
    if line.startswith('TBProfile:'):
      if started:
        if line.startswith("TBProfile: END"):
          finished = True
        else:
          print(line[len('TBProfile: '):-2])  # Strip off the CR LF at the end of each line
      else:
        if line.startswith("TBProfile: START"):
          started = True
  proc.terminate()

def clear_config():
  info("Deleting Tilt Brush.cfg file")
  subprocess.call('adb shell rm -f "%s"' % (tiltbrushcfg), stdout=FNULL)
  info("Done.")

def create_config(sketchname, csv, template, lod):
  if not os.path.exists(template):
    print("Config file '%s' does not exist." % (template))
    return
  configfile = open(template, 'r')
  configtemplate = configfile.read()
  configfile.close()

  temp = tempfile.NamedTemporaryFile(mode='w', delete=False)
  temp.write(configtemplate % (os.path.basename(sketchname), "true" if csv else "false", lod))
  temp.close()

  upload_config(temp.name)
  os.remove(temp.name)

def upload_sketch(sketchname):
  sketchFullPath = "%s/%s" % (sketchpath, os.path.basename(sketchname))
  subprocess.check_call('adb push "%s" "%s"' % (sketchname, sketchFullPath), stdout=FNULL)

def profile_sketches(sketches, reps, csv, configtemplate, lod, skip):
  try:
    enable_powersaving(False)
    save_config()
    signal.signal(signal.SIGINT, sigint_handler)
    if (csv):
      print('Sketch, Num Frames, Min ms, Median ms, Max ms, StdDev, StdDev %, Batches, Triangles, 90fps+, 75fps, 60fps, Sub-60fps')
    for sketch in sketches[skip:]:
      clear_config()
      create_config(sketch, csv, configtemplate, lod)
      upload_sketch(sketch)
      print_header(sketch, csv)
      for i in xrange(0, reps):
        kill()
        run()
        get_profile_data()
        get_screenshot(sketch)
        time.sleep(5)
      print
  finally:
    cleanup()

def upload_config(configFile):
  subprocess.check_call('adb push "%s" "%s"' % (configFile, tiltbrushcfgunescaped), stdout=FNULL)

def upload_config_text(configText):
  temp = tempfile.NamedTemporaryFile(mode='w', delete=False)
  temp.write(configText)
  temp.close()
  subprocess.check_call('adb push "%s" "%s"' % (temp.name, tiltbrushcfgunescaped), stdout=FNULL)

def profile_configs(configs, reps, csv, skip):
  try:
    enable_powersaving(False)
    save_config()
    signal.signal(signal.SIGINT, sigint_handler)
    if csv:
      print('Sketch, Num Frames, Min ms, Median ms, Max ms, StdDev, StdDev %, Batches, Triangles, 90fps+, 75fps, 60fps, Sub-60fps')
    for config in configs[skip:]:
      sketch = None
      sketchName = None
      configFile = open(config, 'r')
      if configFile:
        match = None
        newText = ""
        for line in configFile.readlines():
          match = re.match("""\s*\"SketchToLoad\"\s*\:\s*\"([^\"]+)\"""", line)
          if match:
            sketchName = match.group(1)
            sketchFullPath = "%s/%s" % (sketchpath, os.path.basename(sketchName))
            line = '"SketchToLoad" : "%s",\n' % (sketchFullPath)
          newText += line
        if not sketchName:
          sys.stderr.write("Could not file SketchToLoad in %s\n" % (config))
          continue;
        sketch = find_sketch(sketchName)
        if not sketch:
          sys.stderr.write("Could not load %s" % (os.path.basename(match.group(1))))
          continue
        clear_config()
        upload_config_text(newText)
        upload_sketch(sketch)
        print_header(sketch, csv)
        for i in xrange(0, reps):
          kill()
          run()
          get_profile_data()
          get_screenshot(sketch)
          time.sleep(5)
      else:
        sys.stderr.write("Could not open %s config file." % (config))
  finally:
    cleanup()

def sigint_handler(signal, frame):
  print("SIGINT caught - cleaning up.")
  cleanup()
  sys.exit(0)

def cleanup():
  restore_config()
  enable_powersaving(True)

def print_header(sketch, csv):
  sketch = os.path.basename(sketch)
  if (not csv):
    print('Sketch, %s' % (sketch))


def find_sketch(sketch):
  files = [
    sketch,
    os.path.basename(sketch),
    os.path.dirname(os.path.realpath(__file__)) + "/../Sketches/" + os.path.basename(sketch)]
  for file in files:
    if os.path.exists(file):
      return os.path.realpath(file)
  return None

def expand_globs(globList):
  files = []
  for item in globList:
    files.extend(glob.glob(item))
  return files

def enable_powersaving(enable):
  try:
    subprocess.check_call('adb shell pps-tool.sh %s' % ('enable' if enable else 'disable'),
                          stdout=FNULL)
    info("Set powersaving to %s" % (enable))
  except subprocess.CalledProcessError as err:
    print('Failed to set power saving mode:', err)

def save_config():
  temp = tempfile.NamedTemporaryFile(mode='w', delete=False)
  temp.close()
  os.remove(temp.name);
  subprocess.call('adb pull "%s" "%s"' % (tiltbrushcfgunescaped, temp.name), stdout=FNULL)
  if os.path.exists(temp.name):
    configcopy = temp.name

def restore_config():
  if configcopy:
    upload_config(configcopy)
  else:
    clear_config()

def get_screenshot(filename):
  global screenshotnum
  screenshotFilename = os.path.splitext(os.path.basename(filename))[0] + ".jpg"
  try:
    subprocess.check_call('adb pull "%sTilt Brush/%s" sshot_%d_%s"' %
        (tbdir, screenshotFilename, screenshotnum, screenshotFilename), stdout=FNULL);
    screenshotnum += 1
  except subprocess.CalledProcessError as err:
    # Do nothing on error
    pass

def info(message):
  if (printInfo):
    print (message)

def main():
  import argparse

  parser = argparse.ArgumentParser()
  parser.add_argument('--run', help='Run Tilt Brush on the remote device.', action='store_true')
  parser.add_argument('--kill', help='Kill an existing Tilt Brush process.', action='store_true')
  parser.add_argument('--getprofile', help='Look for and output profile data.', action='store_true')
  parser.add_argument('--quiet', help='Do not print info messages.', action='store_true')
  parser.add_argument('--clearconfig', help='Delete the Tilt Brush.cfg file from device.',
                      action='store_true')
  parser.add_argument('--createconfig', metavar='SKETCHNAME',
                      help='Create a new Tilt brush.cfg file suitable for profiling.', nargs=1)
  parser.add_argument('--uploadsketch', help='Upload a sketch to the device.',
                      nargs=1, metavar='SKETCHNAME')
  parser.add_argument('--profilesketches', help='Profile a set of sketches.', nargs='*')
  parser.add_argument('--profilereps', help='Number of repetitions of a profile', nargs='?',
                      default=1, metavar='REPETITIONS')
  parser.add_argument('--csv', help='Output profile information as csv.', action='store_true',
                      default=False)
  parser.add_argument('--uploadconfig', metavar='CONFIGFILE', help='Upload a config file to device',
                     nargs=1)
  parser.add_argument('--profileconfigs', metavar='CONFIGFILES',
                      help='Profile using a set of config files.', nargs='*')
  parser.add_argument('--powersaving', metavar='ON/OFF',
                      help='Turn power saving on or off.', nargs=1)
  parser.add_argument('--configtemplate', metavar="CONFIG TEMPLATE", nargs='?',
                      default=defaulttemplate, help='A template to use for the Tilt Brush.cfg')
  parser.add_argument('--lod', metavar="CONFIG TEMPLATE", nargs='?',
                      default=99999, help='Global maximum level of detail.')
  parser.add_argument('--skip', metavar="ITEMS TO SKIP", nargs='?', default = 0,
                      help = 'Number of items to skip.')

  args = parser.parse_args()
  global printInfo
  printInfo = not args.quiet
  skip = int(args.skip)

  if args.profilesketches:
    printInfo = False
    profile_sketches(expand_globs(args.profilesketches), int(args.profilereps), args.csv,
                     args.configtemplate, args.lod, skip)
    return
  if args.profileconfigs:
    printInfo = False
    profile_configs(expand_globs(args.profileconfigs), int(args.profilereps), args.csv, skip)
    return
  if args.powersaving:
    enable_powersaving(args.powersaving[0] == 'ON')
  if args.clearconfig:
    clear_config()
  if args.kill:
    kill()
  if args.createconfig:
    create_config(args.createconfig[0], args.csv, args.configtemplate, args.lod)
  if args.uploadconfig:
    upload_config(args.uploadconfig[0])
  if args.uploadsketch:
    upload_sketch(args.uploadsketch[0])
  if args.run:
    run()
  if args.getprofile:
    get_profile_data()

if __name__ == '__main__':
  main()
