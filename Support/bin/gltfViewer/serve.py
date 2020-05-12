#!/usr/bin/python

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

"""Starts a simple http server at given port (default 8000)."""

import os
import SimpleHTTPServer
import SocketServer
import subprocess
import sys
import time

port = 8000
if len(sys.argv) > 1:
  port = int(sys.argv[1])

# If there's another server running at the chosen port, try to kill it.  If that
# fails (e.g. when running on Windows), we forge ahead. TODO: Implement the
# equivalent behavior outside of Linux.
try:
  devnull = open(os.devnull, "w")
  subprocess.check_call(["fuser", "-k", "%s/tcp" % port],
                        stdout=devnull, stderr=subprocess.STDOUT)
  # Give the process a moment to die.
  time.sleep(1)
except (subprocess.CalledProcessError, OSError):
  pass

# Prevents "Address already in use" error when socket lingers in TIME_WAIT,
# even after the corresponding process has been killed.
SocketServer.TCPServer.allow_reuse_address = True

print "Serving at port", port
Handler = SimpleHTTPServer.SimpleHTTPRequestHandler
server = SocketServer.TCPServer(("", port), Handler)
server.serve_forever()
