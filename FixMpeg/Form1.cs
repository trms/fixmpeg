using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FixMpeg
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private string ApplicationName
		{
			get
			{
				FileInfo fi = new FileInfo(Application.ExecutablePath);
				return (Path.GetFileNameWithoutExtension(fi.Name));
			}
		}

		private void ShowMessage(string message)
		{
			listBox1.Items.Add(message);
			listBox1.SelectedIndex = listBox1.Items.Count - 1;
			listBox1.Refresh();
			Application.DoEvents();
		}

		private void CheckFile(string path)
		{
			try
			{
				FileInfo fi = new FileInfo(path);
				ShowMessage(String.Format("Analyzing {0}...", fi.Name));
				string outputPath = Path.Combine(Properties.Settings.Default.OutputDirectory, Path.GetFileNameWithoutExtension(fi.Name)) + ".mpg";
				if (File.Exists(outputPath))
				{
					ShowMessage(String.Format("Error: {0} already exists in output directory, not processing this file.", fi.Name));
					return;
				}
				VideoProcessor vp = new VideoProcessor(path);

				if (vp.VideoFormat == String.Empty)
				{
					ShowMessage(String.Format("{0} is not a video file, skipping", path));
					return;
				}
				else if (vp.AudioFormat == String.Empty)
				{
					ShowMessage(String.Format("{0} appears to be a video file with no audio, skipping", path));
					return;
				}
				ShowMessage(String.Format("{0} is a {1} file at {2}fps and {3}x{4}; audio is {5}, {6} at {7}KHz", fi.Name, vp.VideoFormat, vp.FrameRate, vp.Width, vp.Height, vp.AudioFormat, vp.AudioChannels, vp.AudioRate));

				bool audioOK = false;
				bool videoOK = false;

				if (vp.AudioFormat == "mp2" && vp.AudioRate == 48000 && (vp.AudioChannels == "mono" || vp.AudioChannels == "stereo"))
					audioOK = true;
				if (vp.Height <= 486 && vp.Width <= 720 && vp.FrameRate == 29.97 && vp.VideoFormat == "mpeg2video")
					videoOK = true;

				if (audioOK && videoOK && (uxForceTranscode.Checked == false))
				{
					ShowMessage(String.Format("{0} looks like a valid file, copying it to output directory...", fi.Name));
					File.Copy(path, outputPath);
					return;
				}

				ShowMessage(String.Format("Transcoding {0}...", fi.Name));
				vp.Progress += new VideoProcessor.ProgressCallback(ffmpeg_Progress);
				vp.Transcode((videoOK && !uxForceTranscode.Checked) ? VideoProcessor.OutputVideoFormat.copy : VideoProcessor.OutputVideoFormat.mpeg2,
					Convert.ToInt32(numericUpDown1.Value) * 1000,
					(audioOK && !uxForceTranscode.Checked) ? VideoProcessor.OutputAudioFormat.copy : VideoProcessor.OutputAudioFormat.mp2,
					outputPath);
				ShowMessage(String.Format("Finished processing {0}.", fi.Name));
			}
			catch (Exception ex)
			{
				ShowMessage(String.Format("Error: {0}: {1}", path, ex.Message));
			}
			progressBar1.Value = 0;
		}

		private void ScanDVD(string path)
		{
			int ifoIdx = 1;
			List<IFOParse> ifos = new List<IFOParse>();
			try
			{
				while (true)
				{
					FileInfo fi = new FileInfo(path + Path.DirectorySeparatorChar + "VTS_" + ifoIdx.ToString("00") + "_0.IFO");
					if (fi.Exists == false)
						break;
					ifoIdx++;
					string name = fi.Name.ToUpper();
					int title = Convert.ToInt32(name.Replace("VTS_", "").Replace("_0.IFO", ""));
					IFOParse ifo = new IFOParse(fi.FullName);
					ifos.Add(ifo);
				}
				DVDSelection dvd = new DVDSelection(ifos);
				if (dvd.ShowDialog() == DialogResult.OK)
				{
					for (int i = 0; i < dvd.SelectedNames.Count; i++)
					{
						IFOParse.ProgramChain pgc = dvd.SelectedPGCs[i];
						string outputName = dvd.SelectedNames[i];

						VOBRead vobReader = new VOBRead();
						ShowMessage("Reading from DVD...");
						vobReader.Progress += new VideoProcessor.ProgressCallback(ffmpeg_Progress);
						VOBRead.ElementaryStreams streams = vobReader.ProcessVOB(pgc, Properties.Settings.Default.TempDirectory);

						progressBar1.Value = 0;
						ShowMessage("Remultiplexing DVD files...");
						VideoProcessor vp = new VideoProcessor(streams.AudioFile);
						vp.Progress += new VideoProcessor.ProgressCallback(ffmpeg_Progress);
						vp.RemuxDVD(streams, Path.Combine(Properties.Settings.Default.OutputDirectory, outputName + ".mpg"));

						// clean up
						progressBar1.Value = 0;
						File.Delete(streams.VideoFile);
						File.Delete(streams.AudioFile);
						ShowMessage("Done.");
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		void ffmpeg_Progress(int current, int total)
		{
			Application.DoEvents();
			if(current > total)
				return;
			if (progressBar1.Maximum != total)
				progressBar1.Maximum = total;
			progressBar1.Value = current;
		}

		// recursively scan folders for files to process
		private void ScanFolder(string path)
		{
			Application.DoEvents();
			if (File.Exists(Path.Combine(path, "VIDEO_TS.IFO")))
			{
				ScanDVD(path);
				return;
			}
			if (Directory.Exists(path) == false)
			{
				if (File.Exists(path))
					CheckFile(path);
				return;
			}
			string[] dirs = Directory.GetDirectories(path);
			foreach (string dir in dirs)
			{
				if (new DirectoryInfo(dir).Name.ToLower() == "video_ts" || File.Exists(Path.Combine(dir, "VIDEO_TS.IFO")))
					ScanDVD(dir);
				else
					ScanFolder(dir);
			}
			string[] files = Directory.GetFiles(path);
			foreach (string file in files)
			{
				CheckFile(file);
			}
		}

		private void Form1_DragEnter(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effect = DragDropEffects.Copy;
			}
			else
				e.Effect = DragDropEffects.None;

		}

		private void Form1_DragDrop(object sender, DragEventArgs e)
		{
			Cursor = Cursors.WaitCursor;
			foreach (string filename in (string[])e.Data.GetData(DataFormats.FileDrop))
			{
				ScanFolder(filename);
			}
			Cursor = Cursors.Default;
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			this.Text = ApplicationName;
			if (Properties.Settings.Default.OutputDirectory == null || Properties.Settings.Default.OutputDirectory.Length == 0)
			{
				Properties.Settings.Default.OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
				Properties.Settings.Default.Save();
			}
			if (Properties.Settings.Default.TempDirectory == null || Properties.Settings.Default.TempDirectory.Length == 0)
			{
				if (Utilities.IsRunningOnMono)
					Properties.Settings.Default.TempDirectory = "/tmp";
				else
					Properties.Settings.Default.TempDirectory = Environment.ExpandEnvironmentVariables("%TEMP%");
				Properties.Settings.Default.Save();
			}
			if (Properties.Settings.Default["BitRate"] == null || Properties.Settings.Default.BitRate == 0)
			{
				Properties.Settings.Default.BitRate = 4;
				Properties.Settings.Default.Save();
			}
			linkLabel1.Text = Properties.Settings.Default.OutputDirectory;
			linkLabel2.Text = Properties.Settings.Default.TempDirectory;
			numericUpDown1.Value = Properties.Settings.Default.BitRate;
			comboBox1.SelectedIndex = 0;
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			folderBrowserDialog1.Description = "Select an output directory";
			folderBrowserDialog1.SelectedPath = Properties.Settings.Default.OutputDirectory;
			if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
			{
				Properties.Settings.Default.OutputDirectory = folderBrowserDialog1.SelectedPath;
				linkLabel1.Text = Properties.Settings.Default.OutputDirectory;
				Properties.Settings.Default.Save();
			}
		}

		private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			folderBrowserDialog1.Description = "Select a temporary directory";
			folderBrowserDialog1.SelectedPath = Properties.Settings.Default.TempDirectory;
			if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
			{
				Properties.Settings.Default.TempDirectory = folderBrowserDialog1.SelectedPath;
				linkLabel2.Text = Properties.Settings.Default.TempDirectory;
				Properties.Settings.Default.Save();
			}
		}

		private void numericUpDown1_ValueChanged(object sender, EventArgs e)
		{
			Properties.Settings.Default.BitRate = (int)numericUpDown1.Value;
			Properties.Settings.Default.Save();
		}
	}
}