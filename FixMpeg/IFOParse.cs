using System;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace FixMpeg
{
	/// <summary>
	/// Summary description for IFOParse.
	/// </summary>
	public class IFOParse
	{
		public class Cell
		{
			private int m_duration = 0;
			private int m_partialFrames = 0;
			private int m_frameRate = 0;
			private int m_firstSector = 0;
			private int m_lastSector = 0;
			private int m_vobID = 0;
			private int m_cellID = 0;

			public int Duration { get { return m_duration; } set { m_duration = value; } }
			public int PartialFrames { get { return m_partialFrames; } set { m_partialFrames = value; } }
			public int FrameRate { get { return m_frameRate; } set { m_frameRate = value; } }
			public int FirstSector { get { return m_firstSector; } set { m_firstSector = value; } }
			public int LastSector { get { return m_lastSector; } set { m_lastSector = value; } }
			public int VobID { get { return m_vobID; } set { m_vobID = value; } }
			public int CellID { get { return m_cellID; } set { m_cellID = value; } }
		}

		public class ProgramChain
		{
			private int m_duration = 0;
			private int m_partialFrames = 0;
			private int m_frameRate = 0;
			private int m_angles = 0;
			private List<Cell> m_cells = new List<Cell>();
			private IFOParse m_parent = null;

			public ProgramChain(IFOParse parent)
			{
				m_parent = parent;
			}

			public int Duration { get { return m_duration; } set { m_duration = value; } }
			public int PartialFrames { get { return m_partialFrames; } set { m_partialFrames = value; } }
			public int FrameRate { get { return m_frameRate; } set { m_frameRate = value; } }
			public int Angles { get { return m_angles; } set { m_angles = value; } }
			public List<Cell> Cells { get { return m_cells; } set { m_cells = value; } }
			public IFOParse Title { get { return m_parent; } }
		}

		public class VOB
		{
			private FileInfo m_file = null;
			private long m_sectors = 0;
			private long m_startSector = 0;

			public VOB(string filename)
			{
				m_file = new FileInfo(filename);
				m_sectors = m_file.Length / 2048;
			}

			public long Sectors { get { return m_sectors; } }
			public long FirstSector { get { return m_startSector; } set { m_startSector = value; } }
			public long LastSector
			{
				get
				{
					return m_startSector + m_sectors;
				}
			}
			public string Filename { get { return m_file.FullName; } }
			public FileInfo FileInfo { get { return m_file; } }

		}

		private FileInfo m_file = null;
		private int m_numAudio = 0;
		private string m_audioFormat = "";
		private string m_aspectRatio = "";
		private string m_resolution = "";
		private string m_videoMode = "";
		private List<ProgramChain> m_pgcs = new List<ProgramChain>();
		private List<VOB> m_vobs = new List<VOB>();

		public IFOParse(string filename)
		{
			m_file = new FileInfo(filename);
			FileStream fs = File.OpenRead(filename);
			try
			{
				#region Header
				// verify valid video IFO header
				byte[] id = new byte[12];
				if(fs.Read(id, 0, 12) != 12)
					throw new IOException("Error reading IFO file");
				string idStr = Encoding.ASCII.GetString(id, 0, 12);
				if(idStr != "DVDVIDEO-VTS")
					throw new Exception("Invalid IFO Header");
				#endregion

				#region Version
				// verify IFO version (1.0 or 1.1)
				if(fs.Seek(0x21, SeekOrigin.Begin) != 0x21)
					throw new IOException("Error reading IFO file");
				byte[] version = new byte[1];
				if(fs.Read(version, 0, 1) != 1)
					throw new IOException("Error reading IFO file");
				if(version[0] != 0x10 && version[0] != 0x11)
					throw new Exception("Invalid IFO Header Version");
				#endregion
				
				#region PGC Info
				// find offset to pgc tables
				if(fs.Seek(0xcc, SeekOrigin.Begin) != 0xcc)
					throw new IOException("Error reading IFO file");
				byte[] pgcOffsetData = new byte[4];
				if(fs.Read(pgcOffsetData, 0, 4) != 4)
					throw new IOException("Error reading IFO file");
				int pgcOffset = (pgcOffsetData[0] << 24) + (pgcOffsetData[1] << 16) + (pgcOffsetData[2] << 8) + pgcOffsetData[3];

				// get count of pgcs
				if(fs.Seek(pgcOffset * 2048, SeekOrigin.Begin) != pgcOffset * 2048)
					throw new IOException("Error reading IFO file");
				byte[] pgciData = new byte[2];
				if(fs.Read(pgciData, 0, 2) != 2)
					throw new IOException("Error reading IFO file");
				int pgcCount = (pgciData[0] << 8) + pgciData[1];

				for (int i = 1; i <= pgcCount; i++)
				{
					ProgramChain pgc = new ProgramChain(this);

					// get pgc's info offset within pgc (length is 4 bytes into pgc info)
					if(fs.Seek((pgcOffset * 2048) + (i * 8) + 4, SeekOrigin.Begin) != (pgcOffset * 2048) + (i * 8) + 4)
						throw new IOException("Error reading IFO file");
					byte[] pgcStartData = new byte[4];
					if(fs.Read(pgcStartData, 0, 4) != 4)
						throw new IOException("Error reading IFO file");
					int pgcStart = (pgcStartData[0] << 24) + (pgcStartData[1] << 16) + (pgcStartData[2] << 8) + pgcStartData[3];

					// get pgc's playback time
					if(fs.Seek((pgcOffset * 2048) + pgcStart + 4, SeekOrigin.Begin) != (pgcOffset * 2048) + pgcStart + 4)
						throw new IOException("Error reading IFO file");
					byte[] playbackTimeData = new byte[4];
					if(fs.Read(playbackTimeData, 0, 4) != 4)
						throw new IOException("Error reading IFO file");

					// frame rate
					int frameRate = (playbackTimeData[3] & 0xC0) >> 6;
					if(frameRate == 3)
						pgc.FrameRate = 30;
					else if(frameRate == 1)
						pgc.FrameRate = 25;

					// duration
					int playbackTime = (playbackTimeData[0] << 24) + (playbackTimeData[1] << 16) + (playbackTimeData[2] << 8) + playbackTimeData[3];

					int hour = ((playbackTime >> 28) & 0x0f) * 10 + ((playbackTime >> 24) & 0x0f);
					int min = ((playbackTime >> 20) & 0x0f) * 10 + ((playbackTime >> 16) & 0x0f);
					int sec = ((playbackTime >> 12) & 0x0f) * 10 + ((playbackTime >> 8) & 0x0f);
					int frame = (((playbackTime >> 4) & 0x0f) * 10 + (playbackTime & 0x0f)) - 120;

					pgc.Duration = hour * 3600 + min * 60 + sec;
					pgc.PartialFrames = frame;

					// get pgc's cell playback info table
					if (fs.Seek((pgcOffset * 2048) + pgcStart + 0xE8, SeekOrigin.Begin) != (pgcOffset * 2048) + pgcStart + 0xE8)
						throw new IOException("Error reading IFO file");
					byte[] C_PBKT_buf = new byte[2];
					if (fs.Read(C_PBKT_buf, 0, 2) != 2)
						throw new IOException("Error reading IFO file");
					int C_PBKT = (C_PBKT_buf[0] << 8) + C_PBKT_buf[1];
					if (C_PBKT != 0)
						C_PBKT += (pgcOffset * 2048) + pgcStart;

					// get pgc's cell position info table
					if (fs.Seek((pgcOffset * 2048) + pgcStart + 0xEA, SeekOrigin.Begin) != (pgcOffset * 2048) + pgcStart + 0xEA)
						throw new IOException("Error reading IFO file");
					byte[] C_POST_buf = new byte[2];
					if (fs.Read(C_POST_buf, 0, 2) != 2)
						throw new IOException("Error reading IFO file");
					int C_POST = (C_POST_buf[0] << 8) + C_POST_buf[1];
					if (C_POST != 0)
						C_POST += (pgcOffset * 2048) + pgcStart;

					// get pgc's number of cells
					if (fs.Seek((pgcOffset * 2048) + pgcStart + 3, SeekOrigin.Begin) != (pgcOffset * 2048) + pgcStart + 3)
						throw new IOException("Error reading IFO file");
					byte[] nCells_buf = new byte[1];
					if (fs.Read(nCells_buf, 0, 1) != 1)
						throw new IOException("Error reading IFO file");
					int nCells = nCells_buf[0];
					int nAngles = 1;
					if (C_POST != 0 && C_PBKT != 0)
					{
						for (int nCell = 0; nCell < nCells; nCell++)
						{
							Cell c = new Cell();
							// get info for cell
							/*if (fs.Seek(C_PBKT + 24 * nCell, SeekOrigin.Begin) != C_PBKT + 24 * nCell)
								throw new IOException("Error reading IFO file");
							byte[] iCat_buf = new byte[1];
							if (fs.Read(iCat_buf, 0, 1) != 1)
								throw new IOException("Error reading IFO file");

							// ignore multi-angle
							int iCat = iCat_buf[0] & 0xF10;
							//			0101=First; 1001=Middle ;	1101=Last
							if (iCat == 0x50)
								nAngles = 1;
							else if (iCat == 0x90)
								nAngles++;
							else if (iCat == 0xD0)
							{
								nAngles++;
								break;
							}*/

							#region read duration
							if (fs.Seek(C_PBKT + 24 * nCell + 4, SeekOrigin.Begin) != C_PBKT + 24 * nCell + 4)
								throw new IOException("Error reading IFO file");
							playbackTimeData = new byte[4];
							if (fs.Read(playbackTimeData, 0, 4) != 4)
								throw new IOException("Error reading IFO file");
							playbackTime = (playbackTimeData[0] << 24) + (playbackTimeData[1] << 16) + (playbackTimeData[2] << 8) + playbackTimeData[3];

							hour = ((playbackTime >> 28) & 0x0f) * 10 + ((playbackTime >> 24) & 0x0f);
							min = ((playbackTime >> 20) & 0x0f) * 10 + ((playbackTime >> 16) & 0x0f);
							sec = ((playbackTime >> 12) & 0x0f) * 10 + ((playbackTime >> 8) & 0x0f);
							frame = (((playbackTime >> 4) & 0x0f) * 10 + (playbackTime & 0x0f)) - 120;

							c.Duration = hour * 3600 + min * 60 + sec;
							c.PartialFrames = frame;
							#endregion

							#region entry point
							if (fs.Seek(C_PBKT + 24 * nCell + 8, SeekOrigin.Begin) != C_PBKT + 24 * nCell + 8)
								throw new IOException("Error reading IFO file");
							byte[] entryPointBuf = new byte[4];
							if (fs.Read(entryPointBuf, 0, 4) != 4)
								throw new IOException("Error reading IFO file");
							c.FirstSector = (entryPointBuf[0] << 24) + (entryPointBuf[1] << 16) + (entryPointBuf[2] << 8) + entryPointBuf[3];
							#endregion

							#region last sector
							if (fs.Seek(C_PBKT + 24 * nCell + 20, SeekOrigin.Begin) != C_PBKT + 24 * nCell + 20)
								throw new IOException("Error reading IFO file");
							byte[] lastSectorBuf = new byte[4];
							if (fs.Read(lastSectorBuf, 0, 4) != 4)
								throw new IOException("Error reading IFO file");
							c.LastSector = (lastSectorBuf[0] << 24) + (lastSectorBuf[1] << 16) + (lastSectorBuf[2] << 8) + lastSectorBuf[3];
							#endregion

							if (fs.Seek(C_POST + 4 * nCell, SeekOrigin.Begin) != C_POST + 4 * nCell)
								throw new IOException("Error reading IFO file");
							byte[] vobIDBuf = new byte[2];
							if (fs.Read(vobIDBuf, 0, 2) != 2)
								throw new IOException("Error reading IFO file");
							c.VobID = (vobIDBuf[0] << 8) + vobIDBuf[1];
							if (fs.Seek(C_POST + 4 * nCell + 3, SeekOrigin.Begin) != C_POST + 4 * nCell + 3)
								throw new IOException("Error reading IFO file");
							byte[] cellIDBuf = new byte[1];
							if (fs.Read(cellIDBuf, 0, 1) != 1)
								throw new IOException("Error reading IFO file");
							c.CellID = cellIDBuf[0];

							pgc.Cells.Add(c);
						}
					}
					pgc.Angles = nAngles;
					m_pgcs.Add(pgc);
				}
				#endregion

				#region Video
				if(fs.Seek(0x200, SeekOrigin.Begin) != 0x200)
					throw new IOException("Error reading IFO file");
				byte[] videoAttributes = new byte[2];
				if(fs.Read(videoAttributes, 0, 2) != 2)
					throw new IOException("Error reading IFO file");
				byte va1 = videoAttributes[0];
				byte va2 = videoAttributes[1];

				// video mode
				if(((va1 & 0x30) >> 4) == 0)
					m_videoMode = "NTSC";
				else if(((va1 & 0x30) >> 4) == 1)
					m_videoMode = "PAL";

				// aspect
				if(((va1 & 0xC) >> 2) == 0)
					m_aspectRatio = "4:3";
				else if(((va1 & 0xC) >> 2) == 3)
					m_aspectRatio = "16:9";

				// resolution
				int resolution = ((va2 & 0x38) >> 3);
				switch(resolution)
				{
					case 0:
						m_resolution = "720x480";
						break;
					case 1:
						m_resolution = "704x480";
						break;
					case 2:
						m_resolution = "352x480";
						break;
					case 3:
						m_resolution = "352x240";
						break;
				}
				#endregion

				#region Audio
				// find count of audio streams
				if(fs.Seek(0x203, SeekOrigin.Begin) != 0x203)
					throw new IOException("Error reading IFO file");
				byte[] numAudio = new byte[1];
				if(fs.Read(numAudio, 0, 1) != 1)
					throw new IOException("Error reading IFO file");
				m_numAudio = numAudio[0];
				byte[] audioFormatData = new byte[1];
				if(fs.Read(audioFormatData, 0, 1) != 1)
					throw new IOException("Error reading IFO file");
				int audioFormat = (audioFormatData[0] & 0xe0) >> 5;
				switch(audioFormat)
				{
					case 0:
						m_audioFormat = "AC3";
						break;
					case 2:
						m_audioFormat = "MPEG1";
						break;
					case 3:
						m_audioFormat = "MPEG2";
						break;
					case 4:
						m_audioFormat = "LPCM";
						break;
					case 6:
						m_audioFormat = "DTS";
						break;
				}
				#endregion

				#region VOB File info
				// VTS_##_0.IFO -> VTS_##_1.VOB
				string vobFile = filename.Replace("0.IFO", "1.VOB");
				int idx = vobFile.LastIndexOf("_");
				if (idx == -1)
				{
					// got a straight vob file like "TRAILER.VOB"
					VOB v = new VOB(filename.ToUpper().Replace("IFO", "VOB"));
					m_vobs.Add(v);
				}
				else
				{
					string vobBase = "";
					int vobNum = 0;
					long nextSectors = 0;
					// extract the VOB index # into vobNum
					// vobBase will contain the title's index
					vobBase = vobFile.Substring(0, idx); // up to last _
					vobNum = Convert.ToInt32(vobFile.Substring(idx + 1, 1));
					if (vobNum == 0)
						vobNum++;
					while (true)
					{
						vobFile = vobBase + "_" + vobNum.ToString("0") + ".VOB";
						vobNum++;
						if (File.Exists(vobFile) == false)
							break;
						VOB v = new VOB(vobFile);
						v.FirstSector = nextSectors;
						nextSectors += v.Sectors;
						m_vobs.Add(v);
					}
				}
				#endregion
			}
			finally
			{
				fs.Close();
			}
		}

		public int AudioStreamCount
		{
			get
			{
				return(m_numAudio);
			}
		}

		public List<ProgramChain> ProgramChains
		{
			get
			{
				return (m_pgcs);
			}
		}

		public List<VOB> VOBs
		{
			get
			{
				return (m_vobs);
			}
		}

		public int ProgramChainCount
		{
			get
			{
				return(m_pgcs.Count);
			}
		}

		public string AspectRatio
		{
			get
			{
				return(m_aspectRatio);
			}
		}
	
		public string Resolution
		{
			get
			{
				return(m_resolution);
			}
		}
	
		public string VideoMode
		{
			get
			{
				return(m_videoMode);
			}
		}

		public string AudioFormat
		{
			get
			{
				return(m_audioFormat);
			}
		}

		public string Filename
		{
			get
			{
				return (m_file.FullName);
			}
		}

		public FileInfo FileInfo
		{
			get
			{
				return (m_file);
			}
		}

		public int Length
		{
			get
			{
				int duration = 0;
				int partialFrames = 0;
				foreach (ProgramChain pgc in m_pgcs)
				{
					duration += pgc.Duration;
					partialFrames += pgc.PartialFrames;
				}
				duration += partialFrames % m_pgcs[0].FrameRate;
				return (duration);
			}
		}
	}
}
