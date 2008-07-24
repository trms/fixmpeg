using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FixMpeg
{
	public class VideoProcessor
	{
		private string m_path;
		public delegate void ProgressCallback(int current, int total);
		public event ProgressCallback Progress;
		// format details
		private string m_originalVideoFormat = String.Empty;
		private int m_originalWidth = 0;
		private int m_originalHeight = 0;
		private string m_originalAudioFormat = String.Empty;
		private int m_originalAudioRate = 0;
		private string m_originalAudioChannels = String.Empty;
		private double m_originalFrameRate = 29.97;
		private string m_videoFormat = String.Empty;
		private int m_width = 0;
		private int m_height = 0;
		private string m_audioFormat = String.Empty;
		private int m_audioRate = 0;
		private string m_audioChannels = String.Empty;
		private double m_frameRate = 29.97;
		private bool useAVS = false;
		private string m_avsPath = null;

		public enum OutputVideoFormat
		{
			copy,
			mpeg2,
			mpeg4,
			h264,
			flv,
			wm7,
			wm9
		};

		public enum OutputAudioFormat
		{
			copy,
			mp2,
			mp3
		};

		public VideoProcessor(string path)
		{
			m_path = path;
			//try
			//{
				ScanFileFormatDirect();
			//}
			//catch(Exception ex)
			//{
			//    string ext = new FileInfo(m_path).Extension.ToLower();
			//    if (ext == ".avi" || ext == ".asf" || ext == ".wmv" || ext == ".dv" || ext == ".mov")
			//        ScanFileFormatAVS();
			//    else
			//        throw ex;
			//}
		}

		private string AVSPath
		{
			get
			{
				if(m_avsPath != null)
					return(m_avsPath);
				FileInfo fi = new FileInfo(m_path);
				string path = Path.Combine(fi.DirectoryName, "temp.avs");
				using (StreamWriter sw = new StreamWriter(path))
				{
					sw.WriteLine(String.Format("DirectShowSource(\"{0}\")", m_path));
					sw.Close();
				}
				return (path);
			}
		}

		/*
2.18 How can I read DirectShow files?
If you have built FFmpeg with ./configure --enable-avisynth (only possible on MinGW/Cygwin platforms), then you may use any file that DirectShow can read as input. (Be aware that this feature has been recently added, so you will need to help yourself in case of problems.) 

Just create an "input.avs" text file with this single line ... 

  DirectShowSource("C:\path to your file\yourfile.asf")

... and then feed that text file to FFmpeg: 

  ffmpeg -i input.avs

For ANY other help on Avisynth, please visit http://www.avisynth.org/. 
		*/
		private void ScanFileFormatAVS()
		{
			string details = RunFFMPEG(String.Format("-i \"{0}\"", AVSPath));
			ScanFileFormat(details);
			useAVS = true;
		}

		private void ScanFileFormatDirect()
		{
			string details = RunFFMPEG(String.Format("-i \"{0}\"", m_path));
			ScanFileFormat(details);
		}

		private void ScanFileFormat(string details)
		{
			/*
Input #0, mpeg, from 'file.mpg':
Duration: 00:01:01.0, start: 1.186233, bitrate: 11788 kb/s
Stream #0.0[0x1e0]: Video: mpeg2video, yuv420p, 1280x720 [PAR 1:1 DAR 16:9],
12877 kb/s, 59.94 tb(r)
Stream #0.1[0x80]: Audio: liba52, 48000 Hz, stereo, 384 kb/s
*/
			if (details.ToLower().Contains("could not find codec parameters") || details.ToLower().Contains("unknown format"))
				throw new ArgumentException("Not a recognized file format.");
			Match m;
			m = Regex.Match(details, @"Video: ([^,]*), [^,]*, (\d+)x(\d+)");
			if(!m.Success) // catch weird case that happens sometimes
				m = Regex.Match(details, @"Video: ([^,]*), (\d+)x(\d+)");
			if (m.Success)
			{
				m_videoFormat = m.Groups[1].Value;
				m_width = Convert.ToInt32(m.Groups[2].Value);
				m_height = Convert.ToInt32(m.Groups[3].Value);
			}
			m = Regex.Match(details, @"(\d+\.\d+)\s+tb\(r\)");
			if (m.Success)
			{
				m_frameRate = Convert.ToDouble(m.Groups[1].Value);
			}
			m = Regex.Match(details, @"Audio: ([^,]*), ([^\s]*)\s\w*, ([^,\r\n]*)");
			if (m.Success)
			{
				m_audioFormat = m.Groups[1].Value;
				m_audioRate = Convert.ToInt32(m.Groups[2].Value);
				m_audioChannels = m.Groups[3].Value;
			}

			m_originalVideoFormat = m_videoFormat;
			m_originalWidth = m_width;
			m_originalHeight = m_height;
			m_originalAudioFormat = m_audioFormat;
			m_originalAudioRate = m_audioRate;
			m_originalAudioChannels = m_audioChannels;
			m_originalFrameRate = m_frameRate;
		}

		private string ApplicationDirectory
		{
			get
			{
				FileInfo fi = new FileInfo(Application.ExecutablePath);
				return (fi.DirectoryName);
			}
		}

		public void Transcode(OutputVideoFormat videoFormat, int bitrate, OutputAudioFormat audioFormat, string outputPath)
		{
			string path = m_path;
			if (useAVS)
				path = AVSPath;
			string vcodec = " -vcodec copy";
			// need to support other codecs/options here
			if(videoFormat != OutputVideoFormat.copy)
				vcodec = " -vcodec mpeg2video -b " + bitrate + "k -r 29.97 -s 720x480 -aspect 4:3";
			int outputChannels = 2;
			if (AudioChannels == "mono")
				outputChannels = 1;
			string acodec = " -acodec copy";
			// need to support other formats/options here
			if (audioFormat != OutputAudioFormat.copy)
				acodec = String.Format(" -acodec mp2 -ab 192k -ar 48000 -ac {0}", outputChannels);
			string result = RunFFMPEG(String.Format("-y -i \"{0}\" {1} {2} \"{3}\"", path, vcodec, acodec, outputPath));
			if (result.Contains("Unknown format is not supported as input format"))
				throw new Exception("Can't process this video format");
		}

		public void RemuxDVD(VOBRead.ElementaryStreams streams, string path)
		{
			int outputChannels = 2;
			if (AudioChannels == "mono")
				outputChannels = 1;
			string acodec = String.Format(" -acodec mp2 -ab 192k -ar 48000 -ac {0}", outputChannels);
			RunFFMPEG(String.Format("-y -i \"{0}\" -i \"{1}\" -vcodec copy {2} \"{3}\"", streams.VideoFile, streams.AudioFile, acodec, path));
		}

		private string RunFFMPEG(string parameters)
		{
			Process p = new Process();
			if(Utilities.IsRunningOnMono)
				p.StartInfo.FileName = Path.Combine(ApplicationDirectory, "ffmpeg");
			else
				p.StartInfo.FileName = Path.Combine(ApplicationDirectory, "ffmpeg.exe");
			if (File.Exists(p.StartInfo.FileName) == false)
				throw new IOException(String.Format("Please install ffmpeg.exe into {0}.  It can be downloaded from http://ffdshow.faireal.net/mirror/ffmpeg/", ApplicationDirectory));
			p.StartInfo.Arguments = parameters;
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.RedirectStandardError = true;
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			p.Start();
			string line;
			string result = String.Empty;
			int totalFrames = 0;
			while ((line = p.StandardError.ReadLine()) != null)
			{
				Match m;
				Application.DoEvents();
				result += line + Environment.NewLine;

				if (totalFrames == 0)
				{
					m = Regex.Match(line, @"Duration:\s+(\d+):(\d+):(\d+)\.(\d+)");
					if (m.Success)
					{
						// assume ntsc framerate (29.97)
						int hours = Convert.ToInt32(m.Groups[1].Value);
						int minutes = Convert.ToInt32(m.Groups[2].Value);
						int seconds = Convert.ToInt32(m.Groups[3].Value);
						int frames = Convert.ToInt32(m.Groups[4].Value);

						double total = hours * 3600.0;
						total += minutes * 60.0;
						total += seconds;
						total *= 29.97;
						total += frames;

						totalFrames = Convert.ToInt32(total);
					}
				}

				m = Regex.Match(line, @"frame=\s+(\d+)");
				if (m.Success)
				{
					int currentFrame = Convert.ToInt32(m.Groups[1].Value);
					if (Progress != null)
						Progress(currentFrame, totalFrames);

				}
			}
			if (Progress != null)
				Progress(totalFrames, totalFrames);
			p.WaitForExit();
			p.Dispose();
			return (result);
		}

		public string VideoFormat
		{
			get { return m_videoFormat; }
			set { m_videoFormat = value; }
		}

		public int Width
		{
			get { return m_width; }
			set { m_width = value; }
		}

		public int Height
		{
			get { return m_height; }
			set { m_height = value; }
		}

		public string AudioFormat
		{
			get { return m_audioFormat; }
			set { m_audioFormat = value; }
		}

		public int AudioRate
		{
			get { return m_audioRate; }
			set { m_audioRate = value; }
		}

		public string AudioChannels
		{
			get { return m_audioChannels; }
			set { m_audioChannels = value; }
		}

		public double FrameRate
		{
			get { return m_frameRate; }
			set { m_frameRate = value; }
		}
	}
}
