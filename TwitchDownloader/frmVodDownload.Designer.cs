namespace TwitchDownloader
{
    partial class frmVodDownload
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
            this.label1 = new System.Windows.Forms.Label();
            this.textUrl = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.comboQuality = new System.Windows.Forms.ComboBox();
            this.textFolder = new System.Windows.Forms.TextBox();
            this.textFilename = new System.Windows.Forms.TextBox();
            this.btnFolder = new System.Windows.Forms.Button();
            this.checkCropStart = new System.Windows.Forms.CheckBox();
            this.checkCropEnd = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.labelLength = new System.Windows.Forms.Label();
            this.numStartHour = new System.Windows.Forms.NumericUpDown();
            this.numStartMinute = new System.Windows.Forms.NumericUpDown();
            this.numStartSecond = new System.Windows.Forms.NumericUpDown();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.numEndSecond = new System.Windows.Forms.NumericUpDown();
            this.numEndMinute = new System.Windows.Forms.NumericUpDown();
            this.numEndHour = new System.Windows.Forms.NumericUpDown();
            this.btnGetInfo = new System.Windows.Forms.Button();
            this.pictureThumb = new System.Windows.Forms.PictureBox();
            this.label14 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.textTitle = new System.Windows.Forms.TextBox();
            this.label16 = new System.Windows.Forms.Label();
            this.btnDownload = new System.Windows.Forms.Button();
            this.labelStreamer = new System.Windows.Forms.Label();
            this.labelCreated = new System.Windows.Forms.Label();
            this.textLog = new System.Windows.Forms.TextBox();
            this.backgroundDownloadManager = new System.ComponentModel.BackgroundWorker();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.label6 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numStartHour)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartMinute)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartSecond)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndSecond)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndMinute)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndHour)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureThumb)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(245, 38);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(50, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "VOD ID :";
            // 
            // textUrl
            // 
            this.textUrl.Location = new System.Drawing.Point(301, 35);
            this.textUrl.Name = "textUrl";
            this.textUrl.Size = new System.Drawing.Size(203, 20);
            this.textUrl.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(292, 130);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(45, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Quality :";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(295, 158);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(42, 13);
            this.label3.TabIndex = 3;
            this.label3.Text = "Folder :";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(282, 186);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(55, 13);
            this.label4.TabIndex = 4;
            this.label4.Text = "Filename :";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(272, 214);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(65, 13);
            this.label5.TabIndex = 5;
            this.label5.Text = "Crop Video :";
            // 
            // comboQuality
            // 
            this.comboQuality.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboQuality.Enabled = false;
            this.comboQuality.FormattingEnabled = true;
            this.comboQuality.Location = new System.Drawing.Point(342, 127);
            this.comboQuality.Name = "comboQuality";
            this.comboQuality.Size = new System.Drawing.Size(203, 21);
            this.comboQuality.TabIndex = 7;
            // 
            // textFolder
            // 
            this.textFolder.Enabled = false;
            this.textFolder.Location = new System.Drawing.Point(342, 155);
            this.textFolder.Name = "textFolder";
            this.textFolder.Size = new System.Drawing.Size(169, 20);
            this.textFolder.TabIndex = 8;
            // 
            // textFilename
            // 
            this.textFilename.Enabled = false;
            this.textFilename.Location = new System.Drawing.Point(342, 183);
            this.textFilename.Name = "textFilename";
            this.textFilename.Size = new System.Drawing.Size(203, 20);
            this.textFilename.TabIndex = 9;
            // 
            // btnFolder
            // 
            this.btnFolder.Enabled = false;
            this.btnFolder.Location = new System.Drawing.Point(518, 155);
            this.btnFolder.Name = "btnFolder";
            this.btnFolder.Size = new System.Drawing.Size(28, 20);
            this.btnFolder.TabIndex = 12;
            this.btnFolder.Text = "...";
            this.btnFolder.UseVisualStyleBackColor = true;
            this.btnFolder.Click += new System.EventHandler(this.BtnFolder_Click);
            // 
            // checkCropStart
            // 
            this.checkCropStart.AutoSize = true;
            this.checkCropStart.Enabled = false;
            this.checkCropStart.Location = new System.Drawing.Point(342, 213);
            this.checkCropStart.Name = "checkCropStart";
            this.checkCropStart.Size = new System.Drawing.Size(48, 17);
            this.checkCropStart.TabIndex = 13;
            this.checkCropStart.Text = "Start";
            this.checkCropStart.UseVisualStyleBackColor = true;
            // 
            // checkCropEnd
            // 
            this.checkCropEnd.AutoSize = true;
            this.checkCropEnd.Enabled = false;
            this.checkCropEnd.Location = new System.Drawing.Point(342, 239);
            this.checkCropEnd.Name = "checkCropEnd";
            this.checkCropEnd.Size = new System.Drawing.Size(45, 17);
            this.checkCropEnd.TabIndex = 14;
            this.checkCropEnd.Text = "End";
            this.checkCropEnd.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(26, 258);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(59, 13);
            this.label7.TabIndex = 15;
            this.label7.Text = "VOD Title :";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(291, 106);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(46, 13);
            this.label8.TabIndex = 17;
            this.label8.Text = "Length :";
            // 
            // labelLength
            // 
            this.labelLength.AutoSize = true;
            this.labelLength.Location = new System.Drawing.Point(339, 106);
            this.labelLength.Name = "labelLength";
            this.labelLength.Size = new System.Drawing.Size(49, 13);
            this.labelLength.TabIndex = 18;
            this.labelLength.Text = "00:00:00";
            // 
            // numStartHour
            // 
            this.numStartHour.Enabled = false;
            this.numStartHour.Location = new System.Drawing.Point(407, 210);
            this.numStartHour.Name = "numStartHour";
            this.numStartHour.Size = new System.Drawing.Size(35, 20);
            this.numStartHour.TabIndex = 19;
            // 
            // numStartMinute
            // 
            this.numStartMinute.Enabled = false;
            this.numStartMinute.Location = new System.Drawing.Point(458, 210);
            this.numStartMinute.Name = "numStartMinute";
            this.numStartMinute.Size = new System.Drawing.Size(35, 20);
            this.numStartMinute.TabIndex = 20;
            // 
            // numStartSecond
            // 
            this.numStartSecond.Enabled = false;
            this.numStartSecond.Location = new System.Drawing.Point(510, 210);
            this.numStartSecond.Name = "numStartSecond";
            this.numStartSecond.Size = new System.Drawing.Size(35, 20);
            this.numStartSecond.TabIndex = 21;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Enabled = false;
            this.label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.Location = new System.Drawing.Point(497, 214);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(11, 13);
            this.label10.TabIndex = 22;
            this.label10.Text = ":";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Enabled = false;
            this.label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label11.Location = new System.Drawing.Point(445, 214);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(11, 13);
            this.label11.TabIndex = 23;
            this.label11.Text = ":";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Enabled = false;
            this.label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label12.Location = new System.Drawing.Point(445, 240);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(11, 13);
            this.label12.TabIndex = 28;
            this.label12.Text = ":";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Enabled = false;
            this.label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label13.Location = new System.Drawing.Point(497, 240);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(11, 13);
            this.label13.TabIndex = 27;
            this.label13.Text = ":";
            // 
            // numEndSecond
            // 
            this.numEndSecond.Enabled = false;
            this.numEndSecond.Location = new System.Drawing.Point(510, 236);
            this.numEndSecond.Name = "numEndSecond";
            this.numEndSecond.Size = new System.Drawing.Size(35, 20);
            this.numEndSecond.TabIndex = 26;
            // 
            // numEndMinute
            // 
            this.numEndMinute.Enabled = false;
            this.numEndMinute.Location = new System.Drawing.Point(458, 236);
            this.numEndMinute.Name = "numEndMinute";
            this.numEndMinute.Size = new System.Drawing.Size(35, 20);
            this.numEndMinute.TabIndex = 25;
            // 
            // numEndHour
            // 
            this.numEndHour.Enabled = false;
            this.numEndHour.Location = new System.Drawing.Point(407, 236);
            this.numEndHour.Name = "numEndHour";
            this.numEndHour.Size = new System.Drawing.Size(35, 20);
            this.numEndHour.TabIndex = 24;
            // 
            // btnGetInfo
            // 
            this.btnGetInfo.Location = new System.Drawing.Point(510, 33);
            this.btnGetInfo.Name = "btnGetInfo";
            this.btnGetInfo.Size = new System.Drawing.Size(75, 23);
            this.btnGetInfo.TabIndex = 29;
            this.btnGetInfo.Text = "Get Info";
            this.btnGetInfo.UseVisualStyleBackColor = true;
            this.btnGetInfo.Click += new System.EventHandler(this.BtnGetInfo_Click);
            // 
            // pictureThumb
            // 
            this.pictureThumb.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureThumb.Location = new System.Drawing.Point(22, 78);
            this.pictureThumb.Name = "pictureThumb";
            this.pictureThumb.Size = new System.Drawing.Size(200, 125);
            this.pictureThumb.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureThumb.TabIndex = 30;
            this.pictureThumb.TabStop = false;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(30, 214);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(55, 13);
            this.label14.TabIndex = 31;
            this.label14.Text = "Streamer :";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(22, 236);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(63, 13);
            this.label15.TabIndex = 32;
            this.label15.Text = "Created At :";
            // 
            // textTitle
            // 
            this.textTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textTitle.Location = new System.Drawing.Point(22, 279);
            this.textTitle.Multiline = true;
            this.textTitle.Name = "textTitle";
            this.textTitle.ReadOnly = true;
            this.textTitle.Size = new System.Drawing.Size(200, 63);
            this.textTitle.TabIndex = 33;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(19, 62);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(62, 13);
            this.label16.TabIndex = 34;
            this.label16.Text = "Thumbnail :";
            // 
            // btnDownload
            // 
            this.btnDownload.Enabled = false;
            this.btnDownload.Location = new System.Drawing.Point(377, 276);
            this.btnDownload.Name = "btnDownload";
            this.btnDownload.Size = new System.Drawing.Size(111, 41);
            this.btnDownload.TabIndex = 35;
            this.btnDownload.Text = "Download";
            this.btnDownload.UseVisualStyleBackColor = true;
            this.btnDownload.Click += new System.EventHandler(this.BtnDownload_Click);
            // 
            // labelStreamer
            // 
            this.labelStreamer.AutoSize = true;
            this.labelStreamer.Location = new System.Drawing.Point(91, 214);
            this.labelStreamer.Name = "labelStreamer";
            this.labelStreamer.Size = new System.Drawing.Size(0, 13);
            this.labelStreamer.TabIndex = 36;
            // 
            // labelCreated
            // 
            this.labelCreated.AutoSize = true;
            this.labelCreated.Location = new System.Drawing.Point(91, 236);
            this.labelCreated.Name = "labelCreated";
            this.labelCreated.Size = new System.Drawing.Size(0, 13);
            this.labelCreated.TabIndex = 36;
            // 
            // textLog
            // 
            this.textLog.Location = new System.Drawing.Point(609, 78);
            this.textLog.Multiline = true;
            this.textLog.Name = "textLog";
            this.textLog.Size = new System.Drawing.Size(175, 264);
            this.textLog.TabIndex = 37;
            // 
            // backgroundDownloadManager
            // 
            this.backgroundDownloadManager.WorkerReportsProgress = true;
            this.backgroundDownloadManager.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BackgroundDownloadManager_DoWork);
            this.backgroundDownloadManager.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.BackgroundDownloadManager_ProgressChanged);
            this.backgroundDownloadManager.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BackgroundDownloadManager_RunWorkerCompleted);
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStatus,
            this.toolProgressBar});
            this.statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.statusStrip.Location = new System.Drawing.Point(0, 368);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(812, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 38;
            this.statusStrip.Text = "statusStrip1";
            // 
            // toolStatus
            // 
            this.toolStatus.Name = "toolStatus";
            this.toolStatus.Size = new System.Drawing.Size(26, 17);
            this.toolStatus.Text = "Idle";
            // 
            // toolProgressBar
            // 
            this.toolProgressBar.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.toolProgressBar.Name = "toolProgressBar";
            this.toolProgressBar.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.toolProgressBar.Size = new System.Drawing.Size(200, 16);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(606, 62);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(31, 13);
            this.label6.TabIndex = 39;
            this.label6.Text = "Log :";
            // 
            // frmVodDownload
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(812, 390);
            this.ControlBox = false;
            this.Controls.Add(this.label6);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.textLog);
            this.Controls.Add(this.labelCreated);
            this.Controls.Add(this.labelStreamer);
            this.Controls.Add(this.btnDownload);
            this.Controls.Add(this.label16);
            this.Controls.Add(this.textTitle);
            this.Controls.Add(this.label15);
            this.Controls.Add(this.label14);
            this.Controls.Add(this.pictureThumb);
            this.Controls.Add(this.btnGetInfo);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.numEndSecond);
            this.Controls.Add(this.numEndMinute);
            this.Controls.Add(this.numEndHour);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.numStartSecond);
            this.Controls.Add(this.numStartMinute);
            this.Controls.Add(this.numStartHour);
            this.Controls.Add(this.labelLength);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.checkCropEnd);
            this.Controls.Add(this.checkCropStart);
            this.Controls.Add(this.btnFolder);
            this.Controls.Add(this.textFilename);
            this.Controls.Add(this.textFolder);
            this.Controls.Add(this.comboQuality);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textUrl);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmVodDownload";
            this.Text = "frmVodDownload";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            ((System.ComponentModel.ISupportInitialize)(this.numStartHour)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartMinute)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartSecond)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndSecond)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndMinute)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndHour)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pictureThumb)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textUrl;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboQuality;
        private System.Windows.Forms.TextBox textFolder;
        private System.Windows.Forms.TextBox textFilename;
        private System.Windows.Forms.Button btnFolder;
        private System.Windows.Forms.CheckBox checkCropStart;
        private System.Windows.Forms.CheckBox checkCropEnd;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label labelLength;
        private System.Windows.Forms.NumericUpDown numStartHour;
        private System.Windows.Forms.NumericUpDown numStartMinute;
        private System.Windows.Forms.NumericUpDown numStartSecond;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.NumericUpDown numEndSecond;
        private System.Windows.Forms.NumericUpDown numEndMinute;
        private System.Windows.Forms.NumericUpDown numEndHour;
        private System.Windows.Forms.Button btnGetInfo;
        private System.Windows.Forms.PictureBox pictureThumb;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.TextBox textTitle;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Button btnDownload;
        private System.Windows.Forms.Label labelStreamer;
        private System.Windows.Forms.Label labelCreated;
        private System.Windows.Forms.TextBox textLog;
        private System.ComponentModel.BackgroundWorker backgroundDownloadManager;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStatus;
        private System.Windows.Forms.ToolStripProgressBar toolProgressBar;
        private System.Windows.Forms.Label label6;
    }
}