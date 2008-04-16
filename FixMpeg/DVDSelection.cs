using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace FixMpeg
{
	public partial class DVDSelection : Form
	{
		List<IFOParse> m_ifos = null;
		List<IFOParse.ProgramChain> m_allPgcs = new List<IFOParse.ProgramChain>();
		List<IFOParse.ProgramChain> m_pgcs = new List<IFOParse.ProgramChain>();
		List<string> m_allNames = new List<string>();
		List<string> m_names = new List<string>();

		public DVDSelection(List<IFOParse> ifos)
		{
			InitializeComponent();
			m_ifos = ifos;
		}

		public List<IFOParse.ProgramChain> SelectedPGCs
		{
			get
			{
				return (m_pgcs);
			}
		}

		public List<string> SelectedNames
		{
			get
			{
				return (m_names);
			}
		}

		private void DVDSelection_Load(object sender, EventArgs e)
		{
			string dvdTitle = String.Empty;
			FileInfo fi = new FileInfo(m_ifos[0].Filename);
			if (fi.Directory.Name.ToLower() != "video_ts")
				dvdTitle = fi.Directory.Name;
			else
				dvdTitle = fi.Directory.Parent.Name;

			dvdTitleTxt.Text = dvdTitle;
			this.Text = dvdTitle;

			int item = 0;
			int maxLength = 0;
			int selectTitle = 0;
			for (int title = 0; title < m_ifos.Count; title++)
			{
				IFOParse ifo = m_ifos[title];
				for (int pgcNum = 0; pgcNum < ifo.ProgramChainCount; pgcNum++)
				{
					IFOParse.ProgramChain pgc = ifo.ProgramChains[pgcNum];
					if (pgc.Duration > maxLength)
					{
						selectTitle = item;
						maxLength = pgc.Duration;
					}

					string pgcSummary = String.Format("Title {0}, PGC {1}: {2}", (title + 1), (pgcNum + 1), Utilities.SecondsToLength(pgc.Duration));
					m_allPgcs.Add(pgc);
					m_allNames.Add(String.Format("Title {0}, PGC {1}", (title + 1), (pgcNum + 1)));
					checkedListBox1.Items.Add(pgcSummary);
					item++;
				} 
			}
			// select the longest item (the main title)
			if(checkedListBox1.Items.Count > 0)
				checkedListBox1.SetItemChecked(selectTitle, true);
			checkedListBox1.CheckOnClick = true;
		}

		private void cancelBtn_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
		}

		private void okBtn_Click(object sender, EventArgs e)
		{
			// fill in selected items
			for (int i = 0; i < m_allPgcs.Count; i++)
			{
				if (checkedListBox1.GetItemCheckState(i) == CheckState.Checked)
				{
					m_pgcs.Add(m_allPgcs[i]);
					m_names.Add(String.Format("{0} {1}", dvdTitleTxt.Text, m_allNames[i]));
				}
			}
			this.DialogResult = DialogResult.OK;
		}

		private void dvdTitleTxt_TextChanged(object sender, EventArgs e)
		{
			this.Text = dvdTitleTxt.Text;
		}
	}
}