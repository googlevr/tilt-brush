# !/bin/bash
# Copyright 2020 Google Inc.
#
# Simple FFMPEG Compilation script for Cygwin

cd /
mkdir -p build_ffmpeg
cd build_ffmpeg

# Fetch and build x264
git clone --depth 1 https://code.videolan.org/videolan/x264.git
cd x264
./configure --cross-prefix=x86_64-w64-mingw32- --host=x86_64-w64-mingw32 --prefix="/usr/x86_64-w64-mingw32/sys-root/mingw" --enable-static
make -j$(nproc)
make install 
cd ..

# Fetch and build ffmpeg
git clone --depth 1 https://github.com/FFmpeg/FFmpeg.git ffmpeg
cd ffmpeg
CFLAGS=-I/usr/x86_64-w64-mingw32/sys-root/mingw/include
LDFLAGS=-L/usr/x86_64-w64-mingw32/sys-root/mingw/lib
export PKG_CONFIG_PATH=
export PKG_CONFIG_LIBDIR=/usr/x86_64-w64-mingw32/sys-root/mingw/lib/pkgconfig
./configure --arch=x86_64 --target-os=mingw32 --cross-prefix=x86_64-w64-mingw32- --prefix=/usr/local --pkg-config=pkg-config \
  --pkg-config-flags=--static --extra-cflags=-static --extra-ldflags=-static --extra-libs="-lm -lz -fopenmp" --enable-static \
  --disable-shared --enable-gpl --enable-libx264
make -j$(nproc)
make install
cd ..

cd ..
