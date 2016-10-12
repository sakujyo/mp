arm-linux-androideabi-c++ -pie shrink.c
adb -d push a.out /data/local/tmp/shrink
adb -d shell chmod 766 /data/local/tmp/shrink
adb -d shell /data/local/tmp/shrink /data/local/tmp/sc.raw 3 /data/local/tmp/sh.raw
