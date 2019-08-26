namespace TwitchDownloader
{
    partial class frmClipDownload
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnGetInfo = new System.Windows.Forms.Button();
            this.textUrl = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.comboQuality = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.labelCreated = new System.Windows.Forms.Label();
            this.labelStreamer = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.textTitle = new System.Windows.Forms.TextBox();
            this.label15 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.pictureThumb = new System.Windows.Forms.PictureBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.textLog = new System.Windows.Forms.TextBox();
            this.btnDownload = new System.Windows.Forms.Button();
            this.backgroundDownloadManager = new System.ComponentModel.BackgroundWorker();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStatus = new System.Windows.Forms.ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureThumb)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnGetInfo
            // 
            this.btnGetInfo.Location = new System.Drawing.Point(510, 33);
            this.btnGetInfo.Name = "btnGetInfo";
            this.btnGetInfo.Size = new System.Drawing.Size(75, 23);
            this.btnGetInfo.TabIndex = 32;
            this.btnGetInfo.Text = "Get Info";
            this.btnGetInfo.UseVisualStyleBackColor = true;
            this.btnGetInfo.Click += new System.EventHandler(this.BtnGetInfo_Click);
            // 
            // textUrl
            // 
            this.textUrl.Location = new System.Drawing.Point(301, 35);
            this.textUrl.Name = "textUrl";
            this.textUrl.Size = new System.Drawing.Size(203, 20);
            this.textUrl.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(221, 38);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(74, 13);
            this.label1.TabIndex = 30;
            this.label1.Text = "Clip Slug (ID) :";
            // 
            // comboQuality
            // 
            this.comboQuality.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboQuality.Enabled = false;
            this.comboQuality.FormattingEnabled = true;
            this.comboQuality.Location = new System.Drawing.Point(331, 106);
            this.comboQuality.Name = "comboQuality";
            this.comboQuality.Size = new System.Drawing.Size(194, 21);
            this.comboQuality.TabIndex = 34;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(281, 109);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(45, 13);
            this.label2.TabIndex = 33;
            this.label2.Text = "Quality :";
            // 
            // labelCreated
            // 
            this.labelCreated.AutoSize = true;
            this.labelCreated.Location = new System.Drawing.Point(91, 236);
            this.labelCreated.Name = "labelCreated";
            this.labelCreated.Size = new System.Drawing.Size(0, 13);
            this.labelCreated.TabIndex = 43;
            // 
            // labelStreamer
            // 
            this.labelStreamer.AutoSize = true;
            this.labelStreamer.Location = new System.Drawing.Point(91, 214);
            this.labelStreamer.Name = "labelStreamer";
            this.labelStreamer.Size = new System.Drawing.Size(0, 13);
            this.labelStreamer.TabIndex = 44;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(19, 62);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(62, 13);
            this.label16.TabIndex = 42;
            this.label16.Text = "Thumbnail :";
            // 
            // textTitle
            // 
            this.textTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textTitle.Location = new System.Drawing.Point(22, 279);
            this.textTitle.Multiline = true;
            this.textTitle.Name = "textTitle";
            this.textTitle.ReadOnly = true;
            this.textTitle.Size = new System.Drawing.Size(200, 63);
            this.textTitle.TabIndex = 41;
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(22, 236);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(63, 13);
            this.label15.TabIndex = 40;
            this.label15.Text = "Created At :";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(30, 214);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(55, 13);
            this.label14.TabIndex = 39;
            this.label14.Text = "Streamer :";
            // 
            // pictureThumb
            // 
            this.pictureThumb.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureThumb.Location = new System.Drawing.Point(22, 78);
            this.pictureThumb.Name = "pictureThumb";
            this.pictureThumb.Size = new System.Drawing.Size(200, 125);
            this.pictureThumb.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureThumb.TabIndex = 38;
            this.pictureThumb.TabStop = false;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(32, 258);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(53, 13);
            this.label7.TabIndex = 37;
            this.label7.Text = "Clip Title :";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(606, 62);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(31, 13);
            this.label6.TabIndex = 46;
            this.label6.Text = "Log :";
            // 
            // textLog
            // 
            this.textLog.Location = new System.Drawing.Point(609, 78);
            this.textLog.Multiline = true;
            this.textLog.Name = "textLog";
            this.textLog.Size = new System.Drawing.Size(175, 264);
            this.textLog.TabIndex = 45;
            // 
            // btnDownload
            // 
            this.btnDownload.Enabled = false;
            this.btnDownload.Location = new System.Drawing.Point(368, 214);
            this.btnDownload.Name = "btnDownload";
            this.btnDownload.Size = new System.Drawing.Size(111, 41);
            this.btnDownload.TabIndex = 48;
            this.btnDownload.Text = "Download";
            this.btnDownload.UseVisualStyleBackColor = true;
            this.btnDownload.Click += new System.EventHandler(this.BtnDownload_Click);
            // 
            // backgroundDownloadManager
            // 
            this.backgroundDownloadManager.WorkerReportsProgress = true;
            this.backgroundDownloadManager.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BackgroundDownloadManager_DoWork);
            this.backgroundDownloadManager.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BackgroundDownloadManager_RunWorkerCompleted);
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStatus});
            this.statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.statusStrip.Location = new System.Drawing.Point(0, 368);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(812, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 49;
            this.statusStrip.Text = "statusStrip1";
            // 
            // toolStatus
            // 
            this.toolStatus.Name = "toolStatus";
            this.toolStatus.Size = new System.Drawing.Size(26, 17);
            this.toolStatus.Text = "Idle";
            // 
            // frmClipDownload
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(812, 390);
            this.ControlBox = false;
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.btnDownload);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.textLog);
            this.Controls.Add(this.labelCreated);
            this.Controls.Add(this.labelStreamer);
            this.Controls.Add(this.label16);
            this.Controls.Add(this.textTitle);
            this.Controls.Add(this.label15);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.pictureThumb);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.comboQuality);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnGetInfo);
            this.Controls.Add(this.textUrl);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmClipDownload";
            this.Text = "frmClipDownload";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)(this.pictureThumb)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnGetInfo;
        private System.Windows.Forms.TextBox textUrl;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox comboQuality;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label labelCreated;
        private System.Windows.Forms.Label labelStreamer;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox textTitle;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.PictureBox pictureThumb;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textLog;
        private System.Windows.Forms.Button btnDownload;
        private System.ComponentModel.BackgroundWorker backgroundDownloadManager;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStatus;
    }
}