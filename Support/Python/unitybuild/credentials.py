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

"""Helpers for getting credentials, either from the command line or saved in keyrings."""
from __future__ import print_function

import sys

__all__ = ('get_credential',)

TB_OCULUS_RIFT_APP_ID = '1111640318951750'
TB_OCULUS_QUEST_APP_ID = '2322529091093901'

# External API

def get_credential(name):
  """Returns a Credential object which you can query for its contents."""
  return CREDENTIALS_BY_NAME[name]


# Implementation

def import_keyring():
  """Returns null if unsupported."""
  try:
    import keyring
    return keyring
  except ImportError:
    print("You don't have keyring support. Try running:\npip install keyring pywin32",
          file=sys.stderr)
    return None


class Credential(object):
  KEYRING_USERNAME = 'Tilt Brush Build'

  def __init__(self, name, location, **extra):
    self.name = name % extra
    self.location = None if location is None else location % extra
    self.extra = extra

  def get_secret(self):
    """Fetches a secret from the user's keystore or keyboard.
    Caches the result to the keystore, if possible."""
    keyring = import_keyring()
    if keyring is not None:
      secret = keyring.get_keyring().get_password(self.name, self.KEYRING_USERNAME)
      if secret is not None:
        return secret

    # Pop open Chrome for them
    if self.location is not None:
      import webbrowser
      try: webbrowser.open(self.location)
      except: pass

    # Fetch and cache
    import getpass
    secret = getpass.getpass(prompt="Enter secret for '%s' from\n%s\nPassword: " % (
      self.name, self.location)) or None
    if secret is not None and keyring is not None:
      keyring.get_keyring().set_password(self.name, self.KEYRING_USERNAME, secret)
    return secret

  def set_secret(self, value):
    import keyring
    keyring.get_keyring().set_password(self.name, self.KEYRING_USERNAME, value)

  def delete_secret(self):
    """Returns True if the password existed and was deleted."""
    import keyring
    keyring.get_keyring().delete_password(self.name, self.KEYRING_USERNAME)


CREDENTIALS_BY_NAME = dict(
  (c.name, c) for c in [
    Credential('%(app_id)s',
               'https://dashboard.oculus.com/application/%(app_id)s/api',
               app_id=TB_OCULUS_RIFT_APP_ID),
    Credential('%(app_id)s',
               'https://dashboard.oculus.com/application/%(app_id)s/api',
               app_id=TB_OCULUS_QUEST_APP_ID),
    Credential('Tilt Brush keystore password',
               None),  # Redacted
    Credential('Tilt Brush Oculus Quest signing key password',
               None),  # Redacted
  ])


def main():
  import argparse
  parser = argparse.ArgumentParser()
  parser.add_argument('--delete', action='store_true', help='Delete all existing secrets')
  parser.add_argument('--set', action='store_true', help='Prompt for any unknown secrets')
  args = parser.parse_args()

  keyring = import_keyring()
  if keyring is None:
    print("Aborting.")
    sys.exit(1)

  if args.delete:
    for _, c in sorted(CREDENTIALS_BY_NAME.items()):
      # Delete will fail if it doesn't exist
      try: c.delete_secret()
      except keyring.errors.PasswordDeleteError: pass

  if args.set:
    for _, c in sorted(CREDENTIALS_BY_NAME.items()):
      c.get_secret()


if __name__ == '__main__':
  main()
