using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace FixMpeg
{
	public class VOBRead
	{
		public event VideoProcessor.ProgressCallback Progress;
		private Dictionary<string, FileStream> writers = new Dictionary<string, FileStream>();
		private Dictionary<string, FileStream> readers = new Dictionary<string, FileStream>();
		
		const int BLOCK_START_CODE = 0x000001ba;
		const int BLOCK_CRYPT_MASK = 0x30000000;
		const int VID_DETECT_BYTES = 0x000001e0;
		const int AUD_DETECT_BYTES = 0x000001c0;
		const int AC3_DETECT_BYTES = 0x000001bd;
		const int NAV_DETECT_BYTES = 0x000001bb;
		const int PCI_DETECT_BYTES = 0x000001bf;
		private ByteSwapper bs = null;

		private UInt32 ReadCode(byte[] p, int offset)
		{
			uint x;
			x = p[offset]; x <<= 8;
			x |= p[offset + 1]; x <<= 8;
			x |= p[offset + 2]; x <<= 8;
			x |= p[offset + 3];
			return (x);
		}

		private UInt16 ReadWord(byte[] p, int offset)
		{
			UInt16 x;
			x = p[offset]; x <<= 8;
			x |= p[offset + 1];
			return (x);
		}

		private int ReadPTS(byte[] buf, int offset)
		{
			long a1, a2, a3;
			int pts;

			a1 = (buf[offset] & 0xe) >> 1;
			a2 = ((buf[offset + 1] << 8) | buf[offset + 2]) >> 1;
			a3 = ((buf[offset + 3] << 8) | buf[offset + 4]) >> 1;
			pts = (int)(((a1) << 30) | (a2 << 15) | a3);
			return (pts);
		}

		private void SaveData(FileStream fs, byte[] data, int offset, int len, int id)
		{
			int skip = 0;

			if (fs.Name.EndsWith(".wav")) // swap bytes in wav data
			{
				if (bs == null)
					bs = new ByteSwapper(fs);
				for (int i = offset + skip; i < offset + skip + len; i++)
					bs.NextByte(data[i]);
			}
			else
				fs.Write(data, offset + skip, len);
		}

		private void ReadSector(List<IFOParse.VOB> vobs, byte[] buf, long sector, int offset)
		{
			// find which vob has our sector
			foreach (IFOParse.VOB vob in vobs)
			{
				if (sector >= vob.FirstSector && sector < vob.LastSector)
				{
					// got it
					FileStream fs = null;
					if (readers.ContainsKey(vob.FileInfo.Name))
						fs = (FileStream)readers[vob.FileInfo.Name];
					else
					{
						fs = File.OpenRead(vob.Filename);
						readers[vob.FileInfo.Name] = fs;
					}
					long s = (sector - vob.FirstSector) * 2048 + offset;
					if (fs.Seek(s, SeekOrigin.Begin) != s)
						throw new IOException("Error reading VOB file");
					if (fs.Read(buf, 0, buf.Length) != buf.Length)
						throw new IOException("Error reading VOB file");
					return;
				}
			}
			throw new IOException("Invalid sector");
		}

		private void ReadSector(List<IFOParse.VOB> vobs, byte[] buf, long sector)
		{
			// find which vob has our sector
			foreach (IFOParse.VOB vob in vobs)
			{
				if (sector >= vob.FirstSector && sector < vob.LastSector)
				{
					// got it
					FileStream fs = null;
					if (readers.ContainsKey(vob.FileInfo.Name))
						fs = (FileStream)readers[vob.FileInfo.Name];
					else
					{
						fs = File.OpenRead(vob.Filename);
						readers[vob.FileInfo.Name] = fs;
					}
					long s = (sector - vob.FirstSector) * 2048;
					if (fs.Seek(s, SeekOrigin.Begin) != s)
						throw new IOException("Error reading VOB file");
					if (fs.Read(buf, 0, 2048) != 2048)
						throw new IOException("Error reading VOB file");
					return;
				}
			}
			throw new IOException("Invalid sector");
		}

		private int Offset(List<IFOParse.Cell> cells, List<IFOParse.VOB> vobs)
		{
			int videoOffset = 0;
			int audioOffset = 0;
			byte[] buf = new byte[2048];
			bool haveAudioOffset = false;
			bool haveVideoOffset = false;

			foreach (IFOParse.Cell cell in cells)
			{
				for (int sector = cell.FirstSector; sector < cell.LastSector; sector++)
				{
					ReadSector(vobs, buf, sector);
					uint code = ReadCode(buf, 0);
					if (code != BLOCK_START_CODE)
						continue;

					ulong systemCode = ReadCode(buf, 0x0e);

					switch (systemCode)
					{
						case AUD_DETECT_BYTES:
						case AC3_DETECT_BYTES:
							if ((buf[0x15] & 0x80) != 0 && haveAudioOffset == false)
							{
								audioOffset = ReadPTS(buf, 0x17);
								haveAudioOffset = true;
							}
							break;
						case NAV_DETECT_BYTES:
							if (haveVideoOffset == false)
							{
								videoOffset = (int)ReadCode(buf, 0x39);
								haveVideoOffset = true;
							}
							break;
						default:
							break;
					}
					if (haveAudioOffset && haveVideoOffset)
						break;
				}
				if (haveAudioOffset && haveVideoOffset)
					break;
			}
			foreach (FileStream fs in readers.Values)
			{
				fs.Close();
			}
			readers.Clear();

			int msOffset = audioOffset - videoOffset;
			if (msOffset < 0)
				msOffset -= 44;
			else
				msOffset += 44;
			msOffset /= 90;
			return (msOffset);
		}

		public struct ElementaryStreams
		{
			public string VideoFile;
			public string AudioFile;
		}

		public ElementaryStreams ProcessVOB(IFOParse.ProgramChain pgc, string outputPath)
		{
			ElementaryStreams result = new ElementaryStreams();
			result.AudioFile = null;
			result.VideoFile = null;
			List<IFOParse.VOB> vobs = pgc.Title.VOBs;
			List<IFOParse.Cell> cells = pgc.Cells;

			int audioOffset = 0;
			int videoOffset = 0;
			string audio = null;
			string video = null;

			try
			{
				byte[] buf = new byte[2048];
				int totalSectors = 0;
				int currentSector = 0;
				// find total number of sectors for all cells
				for (int cellNum = 0; cellNum < cells.Count; cellNum++)
				{
					IFOParse.Cell cell = cells[cellNum];
					totalSectors += cell.LastSector - cell.FirstSector;
				}

				for (int cellNum = 0; cellNum < cells.Count; cellNum++)
				{
					IFOParse.Cell cell = cells[cellNum];
					for (int sector = cell.FirstSector; sector <= cell.LastSector; sector++)
					{
						ReadSector(vobs, buf, sector);
						// don't update progress every cell, otherwise it gets *really* slow
						if (currentSector % 3000 == 0 && Progress != null)
							Progress(currentSector, totalSectors);
						currentSector++;
						uint code = ReadCode(buf, 0);
						if (code != BLOCK_START_CODE)
							continue;

						int i = 0x0e;
						ulong systemCode = ReadCode(buf, i);
						i += 4;
						UInt16 headerLength = ReadWord(buf, i);
						i += 2;

						switch (systemCode)
						{
							case AC3_DETECT_BYTES:
								#region write non-mpeg audio data to temp file
								UInt16 flags = ReadWord(buf, i);
								i += 2;
								byte b = buf[i++];
								#region find ms offset
								if ((flags & 0xc000) == 0x8000 && (flags & 0xff) >= 0x80 && audioOffset == 0)
								{
									byte c = buf[i++];
									int offset = (c & 0x0e) << 29;
									offset += (ReadWord(buf, i) & 0xfffe) << 14;
									i += 2;
									offset += (ReadWord(buf, i) >> 1) & 0x7fff;
									i += 2;
									offset /= 90;
									i += b - 5;
									audioOffset = offset;
								}
								else
									i += b;
								#endregion
								byte substream = buf[i++];
								i++; // # frame headers
								UInt16 pointer = ReadWord(buf, i);
								i += 2;
								int t = substream;
								string name = t.ToString("000");
								if (t >= 0xA8) // nothing
									continue;
								else if (t >= 0xA0) // PCM
								{
									// http://dvd.sourceforge.net/dvdinfo/index.html
									name += ".wav";
									i++; // emph, mute, reserved, frame #
									byte details = buf[i++];
									i++; // dynamic range;

									// these seem to be zeroed out in my tests, ignore them
									int bitsPerSample = (details & 0xC0) >> 6;
									int sampleRate = (details & 0x30) >> 4;
									int numChannels = details & 0x07;
									b += 3;
								}
								else if (t >= 0x88) // DTS
									name += ".dts"; // dts audio will be ignored (most dvds are ac3, or at least have an ac3 track)
								else if (t >= 0x80) // AC3
									name += ".ac3";
								else
									continue;
								FileStream w = null;
								if (writers.ContainsKey(name))
									w = (FileStream)writers[name];
								else
								{
									w = File.Create(Path.Combine(outputPath, name));
									writers[name] = w;
									if (name.EndsWith(".wav")) // leave room for wav header
										w.Seek(44, SeekOrigin.Begin);
								}
								SaveData(w, buf, i, (headerLength - 7 - b), t);
								#endregion
								break;
							case VID_DETECT_BYTES:
								#region write mpeg video to temp file
								flags = ReadWord(buf, i);
								i += 2;
								b = buf[i++];
								#region find ms offset
								if ((flags & 0xc000) == 0x8000 && (flags & 0xff) >= 0x80 && videoOffset == 0)
								{
									byte c = buf[i++];
									int offset = (c & 0x0e) << 29;
									offset += (ReadWord(buf, i) & 0xfffe) << 14;
									i += 2;
									offset += (ReadWord(buf, i) >> 1) & 0x7fff;
									i += 2;
									offset /= 90;
									i += b - 5;
									videoOffset = offset;
								}
								else
									i += b;
								#endregion
								string vname = "video.m2v";
								video = vname;
								FileStream vw = null;
								if (writers.ContainsKey(vname))
									vw = (FileStream)writers[vname];
								else
								{
									vw = File.Create(Path.Combine(outputPath, vname));
									writers[vname] = vw;
								}
								SaveData(vw, buf, i, (headerLength - 3 - b), 1);
								#endregion
								break;
							case AUD_DETECT_BYTES:
								#region write mpeg audio to temp file
								flags = ReadWord(buf, i);
								i += 2;
								b = buf[i++];
								#region find ms offset
								if ((flags & 0xc000) == 0x8000 && (flags & 0xff) >= 0x80 && audioOffset == 0)
								{
									byte c = buf[i++];
									int offset = (c & 0x0e) << 29;
									offset += (ReadWord(buf, i) & 0xfffe) << 14;
									i += 2;
									offset += (ReadWord(buf, i) >> 1) & 0x7fff;
									i += 2;
									offset /= 90;
									i += b - 5;
									audioOffset = offset;
								}
								else
									i += b;
								#endregion
								string aname = "vob.mp2";
								if (audio == null)
									audio = aname;
								FileStream aw = null;
								if (writers.ContainsKey(aname))
									aw = (FileStream)writers[aname];
								else
								{
									aw = File.Create(Path.Combine(outputPath, aname));
									writers[aname] = aw;
								}
								SaveData(aw, buf, i, (headerLength - 3 - b), 1);
								#endregion
								break;
							case NAV_DETECT_BYTES:
								#region find vobID and cellID
								int cellID = buf[0x422];
								int vobID = (buf[0x41f] << 8) + buf[0x420];
								#endregion
								break;
							default:
								break;
						}
					}
				}
				if (Progress != null)
					Progress(totalSectors, totalSectors);
			}
			finally
			{
				if (bs != null)
					bs.TRMSFinalize();
				bs = null;
				foreach (string name in writers.Keys)
				{
					FileStream fs = (FileStream)writers[name];
					fs.Close();

					if (name.EndsWith(".m2v"))
						result.VideoFile = Path.Combine(outputPath, name);
					else if (name.EndsWith(".ac3") || name.EndsWith(".dts") || name.EndsWith(".wav") || name.EndsWith(".mp2"))
						result.AudioFile = Path.Combine(outputPath, name);

					// delete anything after the first audio and video streams in the file
					if (audio != null && name != video && name != audio)
						File.Delete(name);
				}
				writers.Clear();
				foreach (FileStream fs in readers.Values)
				{
					fs.Close();
				}
				readers.Clear();
			}
			return (result);
		}
	}
}
