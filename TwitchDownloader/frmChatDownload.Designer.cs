namespace TwitchDownloader
{
    partial class frmChatDownload
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
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.btnDownload = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.textLog = new System.Windows.Forms.TextBox();
            this.labelCreated = new System.Windows.Forms.Label();
            this.labelStreamer = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.textTitle = new System.Windows.Forms.TextBox();
            this.label15 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.pictureThumb = new System.Windows.Forms.PictureBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btnGetInfo = new System.Windows.Forms.Button();
            this.textUrl = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.radioJSON = new System.Windows.Forms.RadioButton();
            this.radioTXT = new System.Windows.Forms.RadioButton();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.numEndSecond = new System.Windows.Forms.NumericUpDown();
            this.numEndMinute = new System.Windows.Forms.NumericUpDown();
            this.numEndHour = new System.Windows.Forms.NumericUpDown();
            this.label11 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.numStartSecond = new System.Windows.Forms.NumericUpDown();
            this.numStartMinute = new System.Windows.Forms.NumericUpDown();
            this.numStartHour = new System.Windows.Forms.NumericUpDown();
            this.checkCropEnd = new System.Windows.Forms.CheckBox();
            this.checkCropStart = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.backgroundDownloadManager = new System.ComponentModel.BackgroundWorker();
            this.statusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureThumb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndSecond)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndMinute)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndHour)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartSecond)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartMinute)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartHour)).BeginInit();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStatus,
            this.toolProgressBar});
            this.statusStrip.LayoutStyle = System.Windows.Forms.ToolStripLayoutStyle.HorizontalStackWithOverflow;
            this.statusStrip.Location = new System.Drawing.Point(0, 368);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(812, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 66;
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
            // btnDownload
            // 
            this.btnDownload.Enabled = false;
            this.btnDownload.Location = new System.Drawing.Point(369, 236);
            this.btnDownload.Name = "btnDownload";
            this.btnDownload.Size = new System.Drawing.Size(111, 41);
            this.btnDownload.TabIndex = 65;
            this.btnDownload.Text = "Download";
            this.btnDownload.UseVisualStyleBackColor = true;
            this.btnDownload.Click += new System.EventHandler(this.BtnDownload_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(606, 62);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(31, 13);
            this.label6.TabIndex = 64;
            this.label6.Text = "Log :";
            // 
            // textLog
            // 
            this.textLog.Location = new System.Drawing.Point(609, 78);
            this.textLog.Multiline = true;
            this.textLog.Name = "textLog";
            this.textLog.Size = new System.Drawing.Size(175, 264);
            this.textLog.TabIndex = 63;
            // 
            // labelCreated
            // 
            this.labelCreated.AutoSize = true;
            this.labelCreated.Location = new System.Drawing.Point(91, 236);
            this.labelCreated.Name = "labelCreated";
            this.labelCreated.Size = new System.Drawing.Size(0, 13);
            this.labelCreated.TabIndex = 61;
            // 
            // labelStreamer
            // 
            this.labelStreamer.AutoSize = true;
            this.labelStreamer.Location = new System.Drawing.Point(91, 214);
            this.labelStreamer.Name = "labelStreamer";
            this.labelStreamer.Size = new System.Drawing.Size(0, 13);
            this.labelStreamer.TabIndex = 62;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(19, 62);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(62, 13);
            this.label16.TabIndex = 60;
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
            this.textTitle.TabIndex = 59;
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(22, 236);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(63, 13);
            this.label15.TabIndex = 58;
            this.label15.Text = "Created At :";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(30, 214);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(55, 13);
            this.label14.TabIndex = 57;
            this.label14.Text = "Streamer :";
            // 
            // pictureThumb
            // 
            this.pictureThumb.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureThumb.Location = new System.Drawing.Point(22, 78);
            this.pictureThumb.Name = "pictureThumb";
            this.pictureThumb.Size = new System.Drawing.Size(200, 125);
            this.pictureThumb.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureThumb.TabIndex = 56;
            this.pictureThumb.TabStop = false;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(52, 258);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(33, 13);
            this.label7.TabIndex = 55;
            this.label7.Text = "Title :";
            // 
            // btnGetInfo
            // 
            this.btnGetInfo.Location = new System.Drawing.Point(510, 33);
            this.btnGetInfo.Name = "btnGetInfo";
            this.btnGetInfo.Size = new System.Drawing.Size(75, 23);
            this.btnGetInfo.TabIndex = 52;
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
            this.label1.Location = new System.Drawing.Point(223, 38);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(72, 13);
            this.label1.TabIndex = 50;
            this.label1.Text = "Clip/VOD ID :";
            // 
            // radioJSON
            // 
            this.radioJSON.AutoSize = true;
            this.radioJSON.Checked = true;
            this.radioJSON.Enabled = false;
            this.radioJSON.Location = new System.Drawing.Point(4, 3);
            this.radioJSON.Name = "radioJSON";
            this.radioJSON.Size = new System.Drawing.Size(105, 17);
            this.radioJSON.TabIndex = 69;
            this.radioJSON.TabStop = true;
            this.radioJSON.Text = "Advanced JSON";
            this.radioJSON.UseVisualStyleBackColor = true;
            // 
            // radioTXT
            // 
            this.radioTXT.AutoSize = true;
            this.radioTXT.Enabled = false;
            this.radioTXT.Location = new System.Drawing.Point(115, 3);
            this.radioTXT.Name = "radioTXT";
            this.radioTXT.Size = new System.Drawing.Size(80, 17);
            this.radioTXT.TabIndex = 70;
            this.radioTXT.Text = "Simple TXT";
            this.radioTXT.UseVisualStyleBackColor = true;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Enabled = false;
            this.label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label12.Location = new System.Drawing.Point(443, 184);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(11, 13);
            this.label12.TabIndex = 83;
            this.label12.Text = ":";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Enabled = false;
            this.label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label13.Location = new System.Drawing.Point(495, 184);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(11, 13);
            this.label13.TabIndex = 82;
            this.label13.Text = ":";
            // 
            // numEndSecond
            // 
            this.numEndSecond.Enabled = false;
            this.numEndSecond.Location = new System.Drawing.Point(508, 180);
            this.numEndSecond.Name = "numEndSecond";
            this.numEndSecond.Size = new System.Drawing.Size(35, 20);
            this.numEndSecond.TabIndex = 81;
            // 
            // numEndMinute
            // 
            this.numEndMinute.Enabled = false;
            this.numEndMinute.Location = new System.Drawing.Point(456, 180);
            this.numEndMinute.Name = "numEndMinute";
            this.numEndMinute.Size = new System.Drawing.Size(35, 20);
            this.numEndMinute.TabIndex = 80;
            // 
            // numEndHour
            // 
            this.numEndHour.Enabled = false;
            this.numEndHour.Location = new System.Drawing.Point(405, 180);
            this.numEndHour.Name = "numEndHour";
            this.numEndHour.Size = new System.Drawing.Size(35, 20);
            this.numEndHour.TabIndex = 79;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Enabled = false;
            this.label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label11.Location = new System.Drawing.Point(443, 158);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(11, 13);
            this.label11.TabIndex = 78;
            this.label11.Text = ":";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Enabled = false;
            this.label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.Location = new System.Drawing.Point(495, 158);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(11, 13);
            this.label10.TabIndex = 77;
            this.label10.Text = ":";
            // 
            // numStartSecond
            // 
            this.numStartSecond.Enabled = false;
            this.numStartSecond.Location = new System.Drawing.Point(508, 154);
            this.numStartSecond.Name = "numStartSecond";
            this.numStartSecond.Size = new System.Drawing.Size(35, 20);
            this.numStartSecond.TabIndex = 76;
            // 
            // numStartMinute
            // 
            this.numStartMinute.Enabled = false;
            this.numStartMinute.Location = new System.Drawing.Point(456, 154);
            this.numStartMinute.Name = "numStartMinute";
            this.numStartMinute.Size = new System.Drawing.Size(35, 20);
            this.numStartMinute.TabIndex = 75;
            // 
            // numStartHour
            // 
            this.numStartHour.Enabled = false;
            this.numStartHour.Location = new System.Drawing.Point(405, 154);
            this.numStartHour.Name = "numStartHour";
            this.numStartHour.Size = new System.Drawing.Size(35, 20);
            this.numStartHour.TabIndex = 74;
            // 
            // checkCropEnd
            // 
            this.checkCropEnd.AutoSize = true;
            this.checkCropEnd.Enabled = false;
            this.checkCropEnd.Location = new System.Drawing.Point(340, 183);
            this.checkCropEnd.Name = "checkCropEnd";
            this.checkCropEnd.Size = new System.Drawing.Size(45, 17);
            this.checkCropEnd.TabIndex = 73;
            this.checkCropEnd.Text = "End";
            this.checkCropEnd.UseVisualStyleBackColor = true;
            // 
            // checkCropStart
            // 
            this.checkCropStart.AutoSize = true;
            this.checkCropStart.Enabled = false;
            this.checkCropStart.Location = new System.Drawing.Point(340, 157);
            this.checkCropStart.Name = "checkCropStart";
            this.checkCropStart.Size = new System.Drawing.Size(48, 17);
            this.checkCropStart.TabIndex = 72;
            this.checkCropStart.Text = "Start";
            this.checkCropStart.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(270, 158);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(61, 13);
            this.label5.TabIndex = 71;
            this.label5.Text = "Crop Time :";
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.radioTXT);
            this.panel2.Controls.Add(this.radioJSON);
            this.panel2.Location = new System.Drawing.Point(330, 107);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(200, 26);
            this.panel2.TabIndex = 87;
            // 
            // backgroundDownloadManager
            // 
            this.backgroundDownloadManager.WorkerReportsProgress = true;
            this.backgroundDownloadManager.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BackgroundDownloadManager_DoWork);
            this.backgroundDownloadManager.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.BackgroundDownloadManager_ProgressChanged);
            this.backgroundDownloadManager.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BackgroundDownloadManager_RunWorkerCompleted);
            // 
            // frmChatDownload
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(812, 390);
            this.ControlBox = false;
            this.Controls.Add(this.panel2);
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
            this.Controls.Add(this.checkCropEnd);
            this.Controls.Add(this.checkCropStart);
            this.Controls.Add(this.label5);
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
            this.Controls.Add(this.btnGetInfo);
            this.Controls.Add(this.textUrl);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmChatDownload";
            this.Text = "frmChatDownload";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureThumb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndSecond)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndMinute)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEndHour)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartSecond)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartMinute)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStartHour)).EndInit();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStatus;
        private System.Windows.Forms.Button btnDownload;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textLog;
        private System.Windows.Forms.Label labelCreated;
        private System.Windows.Forms.Label labelStreamer;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox textTitle;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.PictureBox pictureThumb;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button btnGetInfo;
        private System.Windows.Forms.TextBox textUrl;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.RadioButton radioJSON;
        private System.Windows.Forms.RadioButton radioTXT;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.NumericUpDown numEndSecond;
        private System.Windows.Forms.NumericUpDown numEndMinute;
        private System.Windows.Forms.NumericUpDown numEndHour;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.NumericUpDown numStartSecond;
        private System.Windows.Forms.NumericUpDown numStartMinute;
        private System.Windows.Forms.NumericUpDown numStartHour;
        private System.Windows.Forms.CheckBox checkCropEnd;
        private System.Windows.Forms.CheckBox checkCropStart;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Panel panel2;
        private System.ComponentModel.BackgroundWorker backgroundDownloadManager;
        private System.Windows.Forms.ToolStripProgressBar toolProgressBar;
    }
}