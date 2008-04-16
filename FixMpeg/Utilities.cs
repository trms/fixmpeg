using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FixMpeg
{
	/// <summary>
	/// Byte swap pairs of bytes, and write to file.
	/// </summary>
	public class ByteSwapper
	{
		private FileStream m_fs = null;
		private bool needPair = false;
		private byte prevByte = 0;

		public ByteSwapper(FileStream fs)
		{
			m_fs = fs;
		}

		public void NextByte(byte b)
		{
			byte[] data = new byte[2];
			if(needPair)
			{
				data[0] = b;
				data[1] = prevByte;
				m_fs.Write(data, 0, 2);
				needPair = false;
			}
			else
			{
				prevByte = b;
				needPair = true;
			}
		}

		public void TRMSFinalize()
		{
			byte[] data = new byte[2];
			if(needPair)
			{
				data[0] = 0;
				data[1] = prevByte;
				m_fs.Write(data, 0, 2);
			}
			needPair = false;
		}
	}

	/// <summary>
	/// Summary description for Utilities.
	/// </summary>
	public class Utilities
	{
		public Utilities()
		{
		}

		static public string BytesToSize(ulong bytes)
		{
			if(bytes > 1099511627776)
				return(String.Format("{0:.##} TB", (bytes / 1099511627776.0)));
			if(bytes > 1073741824)
				return(String.Format("{0:.##} GB", (bytes / 1073741824.0)));
			if(bytes > 1048576)
				return(String.Format("{0:.#} MB", (bytes / 1048576.0)));
			if(bytes > 1024)
				return(String.Format("{0} K", (bytes / 1024)));
			return(String.Format("{0} bytes", bytes));
		}

		static public string BytesToSize(long bytes)
		{
			return(BytesToSize((ulong)bytes));
		}

		static public string SecondsToLength(int input) 
		{
			int	inputSecs = input;
			int		seconds		= 0;
			int		minutes		= 0;
			int		hours		= 0;
			string	secondsStr	= "";

			if (inputSecs > 0) 
			{
				seconds = inputSecs % 60;
				inputSecs = inputSecs - seconds;
				if (inputSecs > 0) 
				{
					inputSecs = inputSecs / 60;
					minutes = inputSecs % 60;
					inputSecs = inputSecs - minutes;
					if (inputSecs > 0) 
					{
						inputSecs = inputSecs / 60;
						hours = inputSecs;
					}
				}
				if (hours < 10)
					secondsStr = "0" + hours.ToString() + ":";
				else
					secondsStr = hours.ToString() + ":";

				if (minutes < 10)
					secondsStr += "0" + minutes.ToString() + ":";
				else
					secondsStr += minutes.ToString() + ":";

				if (seconds < 10)
					secondsStr += "0" + seconds.ToString();
				else
					secondsStr += seconds.ToString();
			}
			else
				secondsStr = "00:00:00";

			return secondsStr;
		}

		public string MakeSafeFilename(string file)
		{
			string result = Regex.Replace(file, @"[^\w\d ;'\-_\+=\(\)!@#$%\^&,\.]", "");
			return (result);
		}
	}
}
