FixMpeg
===

**Updated**: Added a checkbox that will allow the user to force files that look ok to be transcoded.

**Updated**: the second release now will process files other than MPEG files (i.e. .wmv, .avi) and will transcode unsupported media to MPEG2 to make them compatible! It will also read and allow you to convert media from a DVD. It does its best to transcode to MPEG2, some low-quality sources may have A/V sync issues, it is good to check the files for any problems after they're converted.

---

FixMpeg is a simple front end to ffmpeg that will convert the audio format and sample rate in an mpeg2 file (.mpg). It is intended to aid the migration of files to the SX Video Servers from Tightrope Media Systems. It will not touch the video, but only convert the audio in a file if it is not correct to stereo MPEG1 layer 2 audio (typical uses are to convert from the wrong sample rate -- 44.1kHz to 48kHz or to convert AC3 audio).

Simply extract the zip file, double click the FixMpeg.exe (not ffmpeg.exe) and then drag a batch of mpeg2 files onto the window. Files that are already correct will be copied, files that can be converted will be converted and you will get a message about any incompatible files.

You can click the path to point it at your desired content directory (it will create a file there with the same name when processing). There's no installer, just place the extracted folder somewhere convenient, and perhaps make a shortcut to FixMpeg.exe someplace even more convenient.

---

FixMpeg will run on MacOSX!  Download mono
(http://www.go-mono.com/mono-downloads/download.html) and install it.  Then,
in in the FixMpeg directory there will be a FixMpeg.zip, double click
it in the Finder to extract it.  Once that finishes, you'll see an
AppleScript called "FixMpeg" (with an icon of a script standing on a square).
Double click the script, it will configure and run FixMpeg for you.
Status isn't perfect yet, but it works.

