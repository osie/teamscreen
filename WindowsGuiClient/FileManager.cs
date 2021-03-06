﻿using ByteSizeLib;
using Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsGuiClient
{
    public partial class FileManager : Form
    {
        protected ClientThread Manager;

        protected String SystemId;

        protected String remoteFolder;

        delegate void fillListRemoteCallback(String actFolder, Boolean parent, String parentPath, List<Model.Listing> entrys);

        public FileManager(String systemId)
        {
            SystemId = systemId;
            InitializeComponent();
        }

        protected List<FileInfo> copyToRemote = new List<FileInfo>();

        protected void fillListRemote(String actFolder, Boolean parent, String parentPath, List<Model.Listing> entrys)
        {
            this.remoteFolder = actFolder;
            this.listRemote.Items.Clear();

            if (parent)
            {
                ListViewItem upper = new ListViewItem("");
                upper.Tag = parentPath;
                upper.SubItems.Add("..");
                upper.SubItems.Add("0");
                upper.SubItems.Add("Folder");
                upper.SubItems.Add("");
                this.listRemote.Items.Add(upper);
            }

            foreach (var entry in entrys)
            {
                ListViewItem lvi = new ListViewItem("");
                lvi.Tag = entry.Path;
                lvi.SubItems.Add(entry.Name);
                
                if (entry.Directory)
                {
                    lvi.SubItems.Add("0");
                    lvi.SubItems.Add("Folder");
                }
                else
                {
                    lvi.SubItems.Add(ByteSize.FromBytes(entry.Size).ToString());
                    lvi.SubItems.Add("File");
                }
                lvi.SubItems.Add(entry.Modified);
                this.listRemote.Items.Add(lvi);
            }
        }

        private void ClientListener_OnFileTransferListing(object sender, Common.EventArgs.FileTransfer.FileTransferListingEventArgs e)
        {
            

            if (!this.IsDisposed && !this.Disposing)
            {
                if (this.InvokeRequired)
                {
                    fillListRemoteCallback d = new fillListRemoteCallback(fillListRemote);
                    this.Invoke(d, new object[] { e.ActFolder, e.Parent, e.ParentPath, e.Entrys });
                }
                else
                {
                    fillListRemote(e.ActFolder, e.Parent, e.ParentPath, e.Entrys);
                }
            }
        }

        private void InitTrees()
        {
            PopulateTreeViewLocal();
            PopulateTreeViewRemote();
        }

        public void setManager(ClientThread manager)
        {
            this.Manager = manager;
            Manager.ClientListener.OnFileTransferListing += ClientListener_OnFileTransferListing;

            InitTrees();
        }

        private void PopulateTreeViewRemote()
        {
            Manager.Manager.sendMessage(new Network.Messages.FileTransfer.Request.ListingMessage {  ClientSystemId = Manager.Manager.SystemId, HostSystemId = this.SystemId });
        }

        private void PopulateTreeViewLocal()
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            PopulateListLocal(new DirectoryInfo(folder));
            PopulateMyDrives(Path.GetPathRoot(folder));
        }

        private void PopulateMyDrives(String selectedDisk)
        {
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            this.selectMyDrives.Items.Clear();
            foreach (DriveInfo d in allDrives)
            {
                if (d.IsReady == true)
                {
                    this.selectMyDrives.Items.Add(d);
                    if(selectedDisk == d.Name)
                    {
                        this.selectMyDrives.SelectedItem = d;
                    }
                }
            }
        }

        public void PopulateListLocal(DirectoryInfo info)
        {
            this.listLocal.Items.Clear();
            if (info.Parent != null)
            {
                ListViewItem upper = new ListViewItem("");
                upper.Tag = info.Parent;
                upper.SubItems.Add("..");
                upper.SubItems.Add("0");
                upper.SubItems.Add("Folder");
                upper.SubItems.Add("");
                this.listLocal.Items.Add(upper);
            }

            foreach (var f in info.GetDirectories())
            {
                ListViewItem lvi = new ListViewItem("");
                lvi.Tag = f;
                lvi.SubItems.Add(f.Name);
                lvi.SubItems.Add("0");
                lvi.SubItems.Add("Folder");
                lvi.SubItems.Add(f.LastWriteTime.ToString());

                this.listLocal.Items.Add(lvi);
            }

            foreach (var f in info.GetFiles())
            {
                ListViewItem lvi = new ListViewItem("");
                lvi.Tag = f;
                lvi.SubItems.Add(f.Name);
                lvi.SubItems.Add(ByteSize.FromBytes(f.Length).ToString());
                lvi.SubItems.Add("File");
                lvi.SubItems.Add(f.LastWriteTime.ToString());

                this.listLocal.Items.Add(lvi);
            }
        }

        private void listLocal_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.listLocal.SelectedItems.Count == 1)
            {
                ListView.SelectedListViewItemCollection items = this.listLocal.SelectedItems;
                
                if(items[0].SubItems[3].Text == "Folder")
                {
                    DirectoryInfo folder = (DirectoryInfo)items[0].Tag;
                    PopulateListLocal(folder);
                }
            }
        }

        private void listRemote_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.listRemote.SelectedItems.Count == 1)
            {
                ListView.SelectedListViewItemCollection items = this.listRemote.SelectedItems;
                
                if (items[0].SubItems[3].Text == "Folder")
                {                    
                    String folder = (String)items[0].Tag;
                    Manager.Manager.sendMessage(new Network.Messages.FileTransfer.Request.ListingMessage { ClientSystemId = Manager.Manager.SystemId, HostSystemId = this.SystemId, Folder = folder });
                }
            }
        }

        private void btnCopyToRemote_Click(object sender, EventArgs e)
        {
            if (this.listLocal.SelectedItems.Count == 1)
            {
                ListView.SelectedListViewItemCollection items = this.listLocal.SelectedItems;

                foreach(ListViewItem item in items) {
                    if (item.SubItems[3].Text == "File")
                    {
                        FileInfo file = (FileInfo)items[0].Tag;
                        this.copyToRemote.Add(file);
                    }
                }
            }

            startCopyToRemote();
        }

        private void btnCopyFromRemote_Click(object sender, EventArgs e)
        {

        }

        private void startCopyToRemote() {

            foreach (var file in this.copyToRemote)
            {
                byte[] buff = File.ReadAllBytes(file.FullName);

                Network.Messages.FileTransfer.Request.CopyMessage msg = new Network.Messages.FileTransfer.Request.CopyMessage();
                msg.ClientSystemId = Manager.Manager.SystemId;
                msg.HostSystemId = this.SystemId;
                msg.Data = buff;
                msg.Name = file.Name;
                msg.Folder = this.remoteFolder;
                
                Manager.Manager.sendMessage(msg);
                
            }
        }

        private void selectMyDrives_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void selectRemoteDrives_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}
