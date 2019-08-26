using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Xabe.FFmpeg;

namespace TwitchDownloader
{
    public partial class frmMain : Form
    {
        frmVodDownload formVodDownload = new frmVodDownload();
        frmClipDownload formClipDownload = new frmClipDownload();
        frmChatDownload formChatDownload = new frmChatDownload();
        frmChatRender formChatRender = new frmChatRender();
        public frmMain()
        {
            InitializeComponent();
        }

        private async void FrmMain_Load(object sender, EventArgs e)
        {
            formVodDownload.TopLevel = false;
            formVodDownload.Dock = DockStyle.Fill;
            formVodDownload.Show();
            mainSplitContainer.Panel2.Controls.Add(formVodDownload);
            formClipDownload.TopLevel = false;
            formClipDownload.Dock = DockStyle.Fill;
            mainSplitContainer.Panel2.Controls.Add(formClipDownload);
            formChatDownload.TopLevel = false;
            formChatDownload.Dock = DockStyle.Fill;
            mainSplitContainer.Panel2.Controls.Add(formChatDownload);
            formChatRender.TopLevel = false;
            formChatRender.Dock = DockStyle.Fill;
            mainSplitContainer.Panel2.Controls.Add(formChatRender);

            formVodDownload.Select();

            await FFmpeg.GetLatestVersion();
        }

        private void FrmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void HideForms()
        {
            formVodDownload.Hide();
            formClipDownload.Hide();
            formChatDownload.Hide();
            formChatRender.Hide();
        }

        private void BtnFrmVodDownload_Click(object sender, EventArgs e)
        {
            HideForms();
            formVodDownload.Show();
        }

        private void BtnFrmClipDownload_Click(object sender, EventArgs e)
        {
            HideForms();
            formClipDownload.Show();
        }

        private void BtnFrmChatDownload_Click(object sender, EventArgs e)
        {
            HideForms();
            formChatDownload.Show();
        }

        private void BtnFrmChatRender_Click(object sender, EventArgs e)
        {
            HideForms();
            formChatRender.Show();
        }

        private void BtnAbout_Click(object sender, EventArgs e)
        {
            frmAbout formAbout = new frmAbout();
            formAbout.Show();
        }
    }
}
