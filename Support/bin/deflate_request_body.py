#!/usr/bin/env python

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

"""Script to check the validity of Requests/NNN_form.file by deflating it."""

import os
import re


class Error(Exception):
  pass


class DebugHeaders(object):
  """Parses the NNN_headers.txt file"""
  def __init__(self, filename):
    self.headers = {}
    txt = file(filename, 'rb').read()
    m = re.search(r'-- Headers follow --', txt)
    if m is None: raise ValueError("No headers in %r" % txt)
    for line in txt[m.end():].lstrip().split('\r\n'):
      if ':' in line:
        key, val = line.split(':', 1)
        self.headers[key] = val[1:]

  def __str__(self):
    return str(self.headers)


def decode_body_part_gzip(body_encoded):
  # https://stackoverflow.com/a/2695575/194921
  import zlib
  return zlib.decompress(body_encoded, 16 + zlib.MAX_WBITS)


def decode_body_part_deflate(body_encoded):
  # https://stackoverflow.com/a/2695466/194921
  import zlib
  # this works for incorrect implementations of deflated content
  try:
    return zlib.decompress(body_encoded, -15)
  except Exception:
    # this works for correct implementations of deflated content
    return zlib.decompress(body_encoded, +15)


def print_request(headers_file, body_file):
  """Pass:
    headers_file - the log file containing headers, usually 'NNN_headers.txt'
    body_file - the log file containing the body, usually 'NNN_form.file'
  """
  headers = DebugHeaders(headers_file).headers
  body_encoded = file(body_file, 'rb').read()
  encoding = headers.get('Content-Encoding')
  try:
    if encoding is None:
      body_decoded = body_encoded
    elif encoding == 'gzip':
      body_decoded = decode_body_part_gzip(body_encoded)
    elif encoding == 'deflate':
      body_decoded = decode_body_part_deflate(body_encoded)
    else:
      raise ValueError("Unknown encoding")
  except Exception as e:
    raise Error("Decode %s %s" % (body_file, encoding), e)

  print headers
  print body_decoded


def quick_print_request(n, prefix='c:/src/tb/Requests'):
  prefix = "%s/%s" % (prefix, n)
  return print_request(prefix + '_headers.txt', prefix + '_form.file')


if __name__ == '__main__':
  try:
    quick_print_request(440)
  except Error as e:
    for x in e.args: print x
