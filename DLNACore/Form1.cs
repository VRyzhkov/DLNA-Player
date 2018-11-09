﻿using System;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DLNAPlayer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        private MediaServer MServer = null;
        private static string ip = "";
        private static int port = 9090;
        private static int trackNum = -1;
        private static bool paused = false;
        private static bool playing = false;
        private static List<String> MediaFileLocation = new List<string> { };
        private static List<int> MediaFileLocationType = new List<int> { };
        private static MemoryStream NextTrack = new MemoryStream();
        private static int trackLoaded = -1;
        string[] mediainfo = { "Unknown", "Unknown" };
        string[] nextMediainfo = { "Unknown", "Unknown" };

        private void ScanDLNARenderers()
        {
            Thread TH = new Thread(() =>
            {
              
                ScanRenderers.Invoke((MethodInvoker)delegate { ScanRenderers.Text = "Scanning... Press to stop"; });
                while (true)
                {
                    Thread.Sleep(1000);
                    DLNA.SSDP.Start();
                    for (int i = 0; i < DLNA.SSDP.Renderers.Count; i++)
                    {
                        String deviceInfo = "";
                        XmlDocument RendererXML = new XmlDocument();
                        try
                        {
                            RendererXML.Load(DLNA.SSDP.Renderers[i]);
                            XmlElement rootXML = RendererXML.DocumentElement;
                            deviceInfo = rootXML.GetElementsByTagName("friendlyName")[0].InnerText;
                        }
                        catch
                        {
                            deviceInfo = DLNA.SSDP.Renderers[i];
                        }
                        if (!MediaRenderers.Items.Contains(deviceInfo))
                            MediaRenderers.Invoke((MethodInvoker)delegate { MediaRenderers.Items.Add(deviceInfo); });
                    }
                }
            });
            TH.Start();
        }
        private void CmdSSDP_Click(object sender, EventArgs e)
        {
            if (playing)
            {
                Stop_Function();
            }
            if (DLNA.SSDP.Run)
            {
                DLNA.SSDP.Stop();
                ScanRenderers.Invoke((MethodInvoker)delegate { ScanRenderers.Text = "Scan Media Renderers"; });
            }
            else
            {
                DLNA.SSDP.Run = true;
                ScanDLNARenderers();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            IPandPortTxt.Text = Extentions.Helper.GetMyIP() + ":9090";
            ApplyServerIPAndPort();
            timer1.Interval = 500;
            decodeOpusToWAVToolStripMenuItem.Checked = Properties.Settings.Default.DecodeOpus;
            decodeFLACToWAVToolStripMenuItem.Checked = Properties.Settings.Default.DecodeFLAC;
            ScanDLNARenderers();
        }

        private void Play()
        {
            if (MediaFiles.SelectedIndex != -1)
            {
                LoadFile(MediaFiles.SelectedIndex);
                trackNum = MediaFiles.SelectedIndex;
            }
            else if (MediaFiles.Items.Count > 0)
            {
                LoadFile();
                trackNum = 0;
                MediaFiles.SelectedIndex = 0;
            }

        }
        private void CmdPlay_Click(object sender, EventArgs e)
        {
            Play();
        }

        private void ClearQueue_Click(object sender, EventArgs e)
        {
            MediaFiles.Items.Clear();
            MediaFileLocation.Clear();
            MediaFileLocationType.Clear();
            trackNum = -1;
            trackLoaded = -1;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] filepath = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string path in filepath)
                if (!Directory.Exists(path))
                {
                    addToList(Path.GetFileName(path), path, 1);
                }
            if (trackLoaded == -1)
                LoadNextTrack(trackNum + 1);
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void ApplyServerIP_Click(object sender, EventArgs e)
        {
            ApplyServerIPAndPort();
        }
        private void ApplyServerIPAndPort()
        {
            string[] parseIPandPort = IPandPortTxt.Text.Split(':');
            ip = parseIPandPort[0];
            port = 9090;
            if (!String.IsNullOrEmpty(parseIPandPort[1]))
                port = Convert.ToInt32(parseIPandPort[1]);
            if (MServer != null) if (MServer.Running) MServer.Stop();
            MServer = new MediaServer(ip, port);
            MServer.Start();
        }

        private void MediaFiles_DoubleClick(object sender, EventArgs e)
        {
            if (MediaFiles.SelectedIndex > -1)
            {
                LoadFile(MediaFiles.SelectedIndex);
                trackNum = MediaFiles.SelectedIndex;
            }
        }
        private void LoadNextTrack(int item)
        {
            string file_to_play = MediaFileLocation[item];
            int location_type = MediaFileLocationType[item];
            string filename = MediaFiles.Items[item].ToString();
            Thread TH = new Thread(() =>
            {
                try
                {
                    Invoke((MethodInvoker)async delegate
                    {
                        NextTrack = new MemoryStream();
                        if (location_type == 1) //local file 
                        {
                            nextMediainfo = Extentions.getMetadata(file_to_play);
                            if (file_to_play.EndsWith(".opus") && decodeOpusToWAVToolStripMenuItem.Checked)
                                NextTrack = Extentions.decodeAudio(file_to_play, 1);
                            else if (file_to_play.EndsWith(".flac") && decodeFLACToWAVToolStripMenuItem.Checked)
                                NextTrack = Extentions.decodeAudio(file_to_play, 2);
                            else
                            {
                                FileStream MediaFile = new FileStream(file_to_play, FileMode.Open);
                                MediaFile.CopyTo(NextTrack);
                                MediaFile.Close();
                            }
                        }
                        else if (location_type == 2) //Google Drive file
                        {
                            GDrive drive = GDriveForm.drive;
                            NextTrack = await drive.DownloadFile(file_to_play);
                            FileStream tempFile = new FileStream("tempfile", FileMode.Create);
                            NextTrack.Position = 0;
                            NextTrack.CopyTo(tempFile);
                            tempFile.Close();
                            nextMediainfo = Extentions.getMetadata("tempfile");
                            if (filename.EndsWith(".opus") && decodeOpusToWAVToolStripMenuItem.Checked)
                            {
                                NextTrack = new MemoryStream();
                                NextTrack = Extentions.decodeAudio("tempfile", 1);
                            }
                            else if (filename.EndsWith(".flac") && decodeFLACToWAVToolStripMenuItem.Checked)
                            {
                                NextTrack = new MemoryStream();
                                NextTrack = Extentions.decodeAudio("tempfile", 2);
                            }
                            File.Delete("tempfile");
                        }
                        else if (location_type == 3) //CD Drive Audio Track
                        {
                            AudioCD drive = CDDriveChooser.drive;
                            NextTrack = drive.getTrack(file_to_play);
                            nextMediainfo[0] = filename;
                            mediainfo[1] = String.Empty;
                        }
                        trackLoaded = item;
                    });
                }
                catch { }
            });
            TH.Start();
        }

        private void LoadFile(int item = 0)
        {
            string file_to_play = MediaFileLocation[item];
            int location_type = MediaFileLocationType[item];
            string filename = MediaFiles.Items[item].ToString();
            int retries = 0;
            Thread TH = new Thread(() =>
            {
                Invoke((MethodInvoker)async delegate
                {
                    if (MediaRenderers.SelectedIndex != -1)
                    {
                        DLNA.DLNADevice Device = new DLNA.DLNADevice(DLNA.SSDP.Renderers[MediaRenderers.SelectedIndex]);
                        if (Device.IsConnected())
                        {
                            if (timer1.Enabled) timer1.Stop();
                            Device.StopPlay();
                            MServer.FS = new MemoryStream();
                            if ((filename.EndsWith(".opus") && decodeOpusToWAVToolStripMenuItem.Checked) || (filename.EndsWith(".flac") && decodeFLACToWAVToolStripMenuItem.Checked))
                                MServer.Filename = "track.wav";
                            else
                                MServer.Filename = filename;
                            string url = null;
                            if (trackNum != trackLoaded)
                            {
                                if (location_type == 1) //local file 
                                {
                                    mediainfo = Extentions.getMetadata(file_to_play);
                                    if (file_to_play.EndsWith(".opus") && decodeOpusToWAVToolStripMenuItem.Checked)
                                        MServer.FS = Extentions.decodeAudio(file_to_play, 1);
                                    else if (file_to_play.EndsWith(".flac") && decodeFLACToWAVToolStripMenuItem.Checked)
                                        MServer.FS = Extentions.decodeAudio(file_to_play, 2);
                                    else
                                    {
                                        FileStream MediaFile = new FileStream(file_to_play, FileMode.Open);
                                        MediaFile.CopyTo(MServer.FS);
                                        MediaFile.Close();
                                    }
                                }
                                else if (location_type == 2) //Google Drive file (local download)
                                {
                                    GDrive drive = GDriveForm.drive;
                                    MServer.FS = await drive.DownloadFile(file_to_play);
                                    FileStream tempFile = new FileStream("tempfile", FileMode.Create);
                                    MServer.FS.Position = 0;
                                    MServer.FS.CopyTo(tempFile);
                                    tempFile.Close();
                                    mediainfo = Extentions.getMetadata("tempfile");
                                    if (filename.EndsWith(".opus") && decodeOpusToWAVToolStripMenuItem.Checked)
                                    {
                                        MServer.FS = new MemoryStream();
                                        MServer.FS = Extentions.decodeAudio("tempfile", 1);
                                    }
                                    else if (filename.EndsWith(".flac") && decodeFLACToWAVToolStripMenuItem.Checked)
                                    {
                                        MServer.FS = new MemoryStream();
                                        MServer.FS = Extentions.decodeAudio("tempfile", 2);
                                    }
                                    File.Delete("tempfile");
                                }
                                else if (location_type == 3) //CD Drive Audio Track
                                {
                                    AudioCD drive = CDDriveChooser.drive;
                                    MServer.FS = drive.getTrack(file_to_play);
                                    mediainfo[0] = filename;
                                    mediainfo[1] = String.Empty;
                                }
                                else if (location_type == 4) //Tidal Track
                                {
                                    Tidl tidl = TidalBrowser.tidl;
                                    url = await tidl.getStreamURL(Convert.ToInt32(file_to_play));
                                    mediainfo = await tidl.getNameAndArtist(Convert.ToInt32(file_to_play));
                                }
                                else if (location_type == 5) //Google Drive file (stream)
                                {
                                    GDrive drive = GDriveForm.drive;
                                    url = await drive.GetUrl(file_to_play);
                                    mediainfo[0] = "Unknown";
                                    mediainfo[1] = "Unknown";
                                }
                            }
                            else
                            {
                                mediainfo = nextMediainfo;
                                MServer.FS = NextTrack;
                                trackLoaded = -1;
                            }
                            Thread.Sleep(100);
                            if (location_type != 4 && location_type != 5)
                                url = "http://" + ip + ":" + port.ToString() + "/track" + Path.GetExtension(MediaFiles.SelectedItem.ToString().Replace('"', '_'));
                            string Reply = Device.TryToPlayFile(url, mediainfo);
                            if (Reply == "OK")
                            {
                                if (!timer1.Enabled) timer1.Start();
                                playing = true;
                                if (MediaFiles.Items.Count - 1 > trackNum)
                                    if (MediaFileLocationType[item + 1] != 4 && MediaFileLocationType[item + 1] != 5)
                                        LoadNextTrack(trackNum + 1);
                            }
                            else
                            {
                                if (retries == 0)
                                {
                                    LoadFile(trackNum);
                                    retries++;
                                }
                                else
                                {
                                    MessageBox.Show("Error playing file");
                                    playing = false;
                                }
                            }

                        }
                    }
                    else
                        MessageBox.Show("No renderer selected");
                });
            });
            TH.Start();
        }

        private void Pause_Click(object sender, EventArgs e)
        {
            Thread TH = new Thread(() =>
            {
                Invoke((MethodInvoker)delegate
                {
                    if (MediaRenderers.SelectedIndex != -1)
                    {
                        DLNA.DLNADevice Device = new DLNA.DLNADevice(DLNA.SSDP.Renderers[MediaRenderers.SelectedIndex]);
                        if (Device.IsConnected())
                        {
                            if (paused)
                            {
                                Device.StartPlay(0);
                                paused = false;
                                Pause.Text = "Pause";
                                if (!timer1.Enabled) timer1.Start();
                            }
                            else
                            {
                                Device.Pause();
                                paused = true;
                                Pause.Text = "Resume";
                                if (timer1.Enabled) timer1.Stop();
                            }
                        }
                    }
                });
            });
            TH.Start();
        }

        private void Stop_Function()
        {
            Thread TH = new Thread(() =>
            {
                Invoke((MethodInvoker)delegate
                {
                    if (MediaRenderers.SelectedIndex != -1)
                    {
                        DLNA.DLNADevice Device = new DLNA.DLNADevice(DLNA.SSDP.Renderers[MediaRenderers.SelectedIndex]);
                        if (Device.IsConnected())
                        {
                            Device.StopPlay();
                            if (timer1.Enabled) timer1.Stop();
                            playing = false;
                        }
                    }
                });
            });
            TH.Start();
        }
        private void Stop_Click(object sender, EventArgs e)
        {
            Stop_Function();
        }

        private void PreviousButton_Click(object sender, EventArgs e)
        {
            if (MediaFiles.Items.Count > 0 && trackNum > 0)
            {
                LoadFile(trackNum - 1);
                MediaFiles.ClearSelected();
                MediaFiles.SelectedIndex = trackNum - 1;
                trackNum--;
            }
        }

        private void NextButton_Click(object sender, EventArgs e)
        {
            PlayNextTrack();
        }

        private void PlayNextTrack()
        {
            if (MediaFiles.Items.Count > 0 && trackNum < MediaFiles.Items.Count - 1)
            {
                trackNum++;
                LoadFile(trackNum);
                MediaFiles.ClearSelected();
                MediaFiles.SelectedIndex = trackNum;
            }
            else
            {
                playing = false;
                trackNum = -1;
            }
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            await Task.Run(() =>
            {
                MediaRenderers.Invoke((MethodInvoker)delegate
                {
                    if (MediaRenderers.SelectedIndex != -1)
                    {
                        DLNA.DLNADevice Device = new DLNA.DLNADevice(DLNA.SSDP.Renderers[MediaRenderers.SelectedIndex]);
                        if (Device.IsConnected())
                        {
                            string info = Device.GetPosition();
                            string trackDurationString = info.ChopOffBefore("<TrackDuration>").Trim().ChopOffAfter("</TrackDuration>");
                            string trackPositionString = info.ChopOffBefore("<RelTime>").Trim().ChopOffAfter("</RelTime>");
                            if (!trackDurationString.Contains("HTTP"))
                                if (trackDurationString.Contains(":") && trackPositionString.Contains(":"))
                                    try
                                    {
                                        TimeSpan trackDurationTimeSpan = TimeSpan.Parse(trackDurationString);
                                        TimeSpan trackPositionTimeStan = TimeSpan.Parse(trackPositionString);
                                        TrackPositionLabel.Invoke((MethodInvoker)delegate { TrackPositionLabel.Text = trackPositionString; });
                                        if (Convert.ToInt32(trackDurationTimeSpan.TotalSeconds) != 0)
                                        {
                                            TrackDurationLabel.Invoke((MethodInvoker)delegate { TrackDurationLabel.Text = trackDurationString; });
                                            trackProgress.Invoke((MethodInvoker)delegate { trackProgress.Maximum = Convert.ToInt32(trackDurationTimeSpan.TotalSeconds); trackProgress.Value = Convert.ToInt32(trackPositionTimeStan.TotalSeconds); });
                                            if (Convert.ToInt32(trackDurationTimeSpan.TotalSeconds) - Convert.ToInt32(trackPositionTimeStan.TotalSeconds) <= 2)
                                            {
                                                Thread.Sleep(2000);
                                                timer1.Stop();
                                                PlayNextTrack();
                                            }
                                        }
                                        else
                                        {
                                            TrackDurationLabel.Invoke((MethodInvoker)delegate { TrackDurationLabel.Text = ""; });
                                        }
                                    }
                                    catch { }
                        }
                    }
                });
            });
        }

        private void trackProgress_MouseUp(object sender, MouseEventArgs e)
        {
            if (MediaRenderers.SelectedIndex != -1)
            {
                Thread TH = new Thread(() =>
                {
                    Invoke((MethodInvoker)delegate
                    {

                        TimeSpan positionToGo = TimeSpan.FromSeconds(trackProgress.Value);
                        DLNA.DLNADevice Device = new DLNA.DLNADevice(DLNA.SSDP.Renderers[MediaRenderers.SelectedIndex]);
                        if (Device.IsConnected())
                            Device.Seek(String.Format("{0:c}", positionToGo));
                    });
                });
                TH.Start();
            }
        }

        public void addToList(string item, string location, int type)
        {
            MediaFiles.Items.Add(item);
            MediaFileLocation.Add(location);
            MediaFileLocationType.Add(type);
        }

        private void googleDriveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GDriveForm DriveForm = new GDriveForm()
            {
                Owner = this
            };
            DriveForm.Show();
        }

        private void MediaFiles_KeyUp(object sender, KeyEventArgs e)
        {
            if (MediaFiles.SelectedIndex > -1)
                if (e.KeyCode == Keys.Delete)
                {
                    int firstItem = MediaFiles.SelectedIndices[0];
                    int lastItem = MediaFiles.SelectedIndices[MediaFiles.SelectedIndices.Count - 1];
                    if (trackNum >= firstItem)
                    {
                        if (trackLoaded == trackNum + 1)
                        {
                            trackLoaded = firstItem;
                        }
                        trackNum = firstItem - 1;
                    }

                    for (int i = lastItem; i >= firstItem; i--)
                    {
                        MediaFiles.Items.RemoveAt(i);
                        MediaFileLocation.RemoveAt(i);
                        MediaFileLocationType.RemoveAt(i);

                    }
                    if (MediaFiles.Items.Count == 0)
                        trackLoaded = -1;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    Play();
                }
                else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
                    for (int i = 0; i < MediaFiles.Items.Count; i++)
                        MediaFiles.SetSelected(i, true);
        }

        private void openFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog
            {
                Title = "Browse for media files",
                FileName = "",
                Filter = "Media files|*.*",
                Multiselect = true,
            };
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                if (fileDialog.FileNames != null)
                {
                    foreach (string path in fileDialog.FileNames)
                        if (!Directory.Exists(path))
                            addToList(Path.GetFileName(path), path, 1);
                    if (trackLoaded == -1)
                        LoadNextTrack(trackNum + 1);
                }
            }

        }

        private void readmeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/moisesmcardona/DLNA-Player/blob/master/README.md");
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("GUI created by Moisés Cardona" + Environment.NewLine +
              "Version 0.5" + Environment.NewLine +
              "GitHub: https://github.com/moisesmcardona/DLNA-Player" + Environment.NewLine + Environment.NewLine +
              "This software contains code based on the following Open Source code from CodeProject:" + Environment.NewLine +
              "DLNAMediaServer: https://www.codeproject.com/Articles/1079847/DLNA-Media-Server-to-feed-Smart-TVs" + Environment.NewLine +
              "DLNACore: https://www.codeproject.com/articles/893791/dlna-made-easy-with-play-to-from-any-device" + Environment.NewLine +
              "C Sharp Ripper: https://www.codeproject.com/articles/5458/c-sharp-ripper");
        }

        private void openAudioCDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CDDriveChooser driveChooser = new CDDriveChooser()
            {
                Owner = this
            };
            driveChooser.Show();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (var process in Process.GetProcessesByName("DLNAPlayer"))
            {
                process.Kill();
            }
        }

        private void tidalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TidalBrowser tidal = new TidalBrowser()
            {
                Owner = this
            };
            try
            {
                tidal.Show();
            }
            catch { }
        }

        private void decodeOpusToWAVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!decodeOpusToWAVToolStripMenuItem.Checked)
                decodeOpusToWAVToolStripMenuItem.Checked = true;
            else
                decodeOpusToWAVToolStripMenuItem.Checked = false;
            Properties.Settings.Default.DecodeOpus = decodeOpusToWAVToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }

        private void decodeFLACToWAVToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!decodeFLACToWAVToolStripMenuItem.Checked)
                decodeFLACToWAVToolStripMenuItem.Checked = true;
            else
                decodeFLACToWAVToolStripMenuItem.Checked = false;
            Properties.Settings.Default.DecodeFLAC = decodeFLACToWAVToolStripMenuItem.Checked;
            Properties.Settings.Default.Save();
        }
    }
}
