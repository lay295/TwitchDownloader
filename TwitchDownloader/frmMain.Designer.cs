namespace TwitchDownloader
{
    partial class frmMain
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMain));
            this.mainSplitContainer = new System.Windows.Forms.SplitContainer();
            this.btnAbout = new System.Windows.Forms.Button();
            this.btnFrmChatRender = new System.Windows.Forms.Button();
            this.btnFrmChatDownload = new System.Windows.Forms.Button();
            this.btnFrmClipDownload = new System.Windows.Forms.Button();
            this.btnFrmVodDownload = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).BeginInit();
            this.mainSplitContainer.Panel1.SuspendLayout();
            this.mainSplitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainSplitContainer
            // 
            this.mainSplitContainer.IsSplitterFixed = true;
            this.mainSplitContainer.Location = new System.Drawing.Point(12, 12);
            this.mainSplitContainer.Name = "mainSplitContainer";
            this.mainSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // mainSplitContainer.Panel1
            // 
            this.mainSplitContainer.Panel1.Controls.Add(this.btnAbout);
            this.mainSplitContainer.Panel1.Controls.Add(this.btnFrmChatRender);
            this.mainSplitContainer.Panel1.Controls.Add(this.btnFrmChatDownload);
            this.mainSplitContainer.Panel1.Controls.Add(this.btnFrmClipDownload);
            this.mainSplitContainer.Panel1.Controls.Add(this.btnFrmVodDownload);
            this.mainSplitContainer.Panel1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            // 
            // mainSplitContainer.Panel2
            // 
            this.mainSplitContainer.Panel2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.mainSplitContainer.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.mainSplitContainer.Size = new System.Drawing.Size(812, 461);
            this.mainSplitContainer.SplitterDistance = 67;
            this.mainSplitContainer.SplitterWidth = 1;
            this.mainSplitContainer.TabIndex = 1;
            // 
            // btnAbout
            // 
            this.btnAbout.Location = new System.Drawing.Point(734, 3);
            this.btnAbout.Name = "btnAbout";
            this.btnAbout.Size = new System.Drawing.Size(75, 23);
            this.btnAbout.TabIndex = 8;
            this.btnAbout.Text = "About";
            this.btnAbout.UseVisualStyleBackColor = true;
            this.btnAbout.Click += new System.EventHandler(this.BtnAbout_Click);
            // 
            // btnFrmChatRender
            // 
            this.btnFrmChatRender.Location = new System.Drawing.Point(557, 9);
            this.btnFrmChatRender.Name = "btnFrmChatRender";
            this.btnFrmChatRender.Size = new System.Drawing.Size(142, 49);
            this.btnFrmChatRender.TabIndex = 7;
            this.btnFrmChatRender.Text = "Chat Render";
            this.btnFrmChatRender.UseVisualStyleBackColor = true;
            this.btnFrmChatRender.Click += new System.EventHandler(this.BtnFrmChatRender_Click);
            // 
            // btnFrmChatDownload
            // 
            this.btnFrmChatDownload.Location = new System.Drawing.Point(409, 9);
            this.btnFrmChatDownload.Name = "btnFrmChatDownload";
            this.btnFrmChatDownload.Size = new System.Drawing.Size(142, 49);
            this.btnFrmChatDownload.TabIndex = 6;
            this.btnFrmChatDownload.Text = "Chat Downloader";
            this.btnFrmChatDownload.UseVisualStyleBackColor = true;
            this.btnFrmChatDownload.Click += new System.EventHandler(this.BtnFrmChatDownload_Click);
            // 
            // btnFrmClipDownload
            // 
            this.btnFrmClipDownload.Location = new System.Drawing.Point(261, 9);
            this.btnFrmClipDownload.Name = "btnFrmClipDownload";
            this.btnFrmClipDownload.Size = new System.Drawing.Size(142, 49);
            this.btnFrmClipDownload.TabIndex = 5;
            this.btnFrmClipDownload.Text = "Clip Downloader";
            this.btnFrmClipDownload.UseVisualStyleBackColor = true;
            this.btnFrmClipDownload.Click += new System.EventHandler(this.BtnFrmClipDownload_Click);
            // 
            // btnFrmVodDownload
            // 
            this.btnFrmVodDownload.Location = new System.Drawing.Point(113, 9);
            this.btnFrmVodDownload.Name = "btnFrmVodDownload";
            this.btnFrmVodDownload.Size = new System.Drawing.Size(142, 49);
            this.btnFrmVodDownload.TabIndex = 4;
            this.btnFrmVodDownload.Text = "VOD Downlaoder";
            this.btnFrmVodDownload.UseVisualStyleBackColor = true;
            this.btnFrmVodDownload.Click += new System.EventHandler(this.BtnFrmVodDownload_Click);
            // 
            // frmMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(836, 472);
            this.Controls.Add(this.mainSplitContainer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frmMain";
            this.Text = "Twitch Downloader";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FrmMain_FormClosing);
            this.Load += new System.EventHandler(this.FrmMain_Load);
            this.mainSplitContainer.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).EndInit();
            this.mainSplitContainer.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.SplitContainer mainSplitContainer;
        private System.Windows.Forms.Button btnFrmChatRender;
        private System.Windows.Forms.Button btnFrmChatDownload;
        private System.Windows.Forms.Button btnFrmClipDownload;
        private System.Windows.Forms.Button btnFrmVodDownload;
        private System.Windows.Forms.Button btnAbout;
    }
}

