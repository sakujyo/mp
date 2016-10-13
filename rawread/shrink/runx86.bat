REM set path=%path%;\android-ndk-r13\build\tools
REM make_standalone_toolchain.py --arch x86 --install-dir=/ndk-toolchain-x86
REM set path=%path%;\ndk-toolchain-x86\bin
i686-linux-android-c++ -pie shrink.c
adb -s 192.168.56.101:5555 push a.out /data/local/tmp/shrink
adb -s 192.168.56.101:5555 shell chmod 766 /data/local/tmp/shrink
adb -s 192.168.56.101:5555 shell /data/local/tmp/shrink /data/local/tmp/sc.raw 3 /data/local/tmp/sh.raw
