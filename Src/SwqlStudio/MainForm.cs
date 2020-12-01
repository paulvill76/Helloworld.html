﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Web;
using System.Windows.Forms;
using SolarWinds.InformationService.Contract2;
using SolarWinds.InformationService.InformationServiceClient;
using SwqlStudio.Metadata;
using SwqlStudio.Properties;
using SwqlStudio.Subscriptions;
using SwqlStudio.Utils;
using WeifenLuo.WinFormsUI.Docking;

namespace SwqlStudio
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single,
        ConcurrencyMode = ConcurrencyMode.Multiple,
        UseSynchronizationContext = false)]
    internal partial class MainForm : Form, IApplicationService
    {
        private static readonly SolarWinds.Logging.Log log = new SolarWinds.Logging.Log();
        private ServerList serverList;
        private ConnectionsManager connectionsManager;

        public PropertyBag QueryParameters
        {
            get { return this.filesDock.QueryParameters; }
            set { this.filesDock.QueryParameters = value; }
        }
        
        public SubscriptionManager SubscriptionManager { get; } = new SubscriptionManager();

        public ConnectionInfo SelectedConnection
        {
            get { return this.connectionsCombobox.SelectedItem as ConnectionInfo; }
        }

        public MainForm()
        {
            InitializeComponent();

            InitializeDockPanel();
            SetEntityGroupingMode((EntityGroupingMode)Enum.Parse(typeof(EntityGroupingMode), Settings.Default.EntityGroupingMode));

            startTimer.Enabled = true;

            SubscriptionManager = new SubscriptionManager();
        }

        private void InitializeDockPanel()
        {
            var connectionsDropDown = this.connectionsCombobox.ComboBox;
            connectionsDropDown.DisplayMember = "Title";
            this.filesDock.SetObjectExplorerImageList(this.ObjectExplorerImageList);
            this.serverList = new ServerList();
            this.serverList.ConnectionsChanged += ServerListOnConnectionsChanged;
            this.connectionsManager = new ConnectionsManager(this, this.serverList, this.filesDock);
            var tabsFactory = new TabsFactory(this.filesDock, this, this.serverList, this.connectionsManager);
            this.filesDock.SetAplicationService(tabsFactory);
            this.filesDock.ActiveContentChanged += FilesDock_ActiveContentChanged;
        }

        private void FilesDock_ActiveContentChanged(object sender, EventArgs e)
        {
            this.RefreshSelectedConnections();

            IConnectionTab activeConnectionTab = this.filesDock.ActiveConnectionTab;
            if (activeConnectionTab != null)
            {
                this.connectionsCombobox.SelectedItem = activeConnectionTab.ConnectionInfo;
            }
        }

        public void RefreshSelectedConnections()
        {
            IConnectionTab activeConnectionTab = this.filesDock.ActiveConnectionTab;
            this.connectionsCombobox.Enabled = activeConnectionTab == null || activeConnectionTab.AllowsChangeConnection;
        }

        private void ServerListOnConnectionsChanged(object sender, EventArgs eventArgs)
        {
            var connectionsDropDown = this.connectionsCombobox.ComboBox;
            var lastSelected = this.connectionsCombobox.SelectedItem;
            List<ConnectionInfo> serverListConnections = this.serverList.Connections;
            connectionsDropDown.DataSource = new BindingList<ConnectionInfo>(serverListConnections);
            
            if(lastSelected == null && serverListConnections.Any())
                lastSelected = serverListConnections.First();
            
            this.connectionsCombobox.SelectedItem = lastSelected;
        }

        private void startTimer_Tick(object sender, EventArgs e)
        {
            startTimer.Enabled = false;
            this.filesDock.AddNewQueryTab();
        }

        #region Code related to File menu

        private void menuFileNew_Click(object sender, EventArgs e)
        {
            this.filesDock.AddNewQueryTab();
        }

        private void menuFileOpen_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
                this.filesDock.OpenFiles(openFileDialog.FileNames);
        }

        
        private void menuFileClose_Click(object sender, EventArgs e)
        {
            this.filesDock.CloseActiveContent();
        }

        private void menuFileSave_Click(object sender, EventArgs e)
        {
            var editor = this.filesDock.ActiveQueryTab;
            if (editor != null)
                DoSave(editor);
        }

        private bool DoSave(QueryTab editor)
        {
            if (string.IsNullOrEmpty(editor.FileName))
                return DoSaveAs(editor);
                
            return SaveEditor(editor, editor.FileName);
        }

        private void menuFileSaveAs_Click(object sender, EventArgs e)
        {
            var editor = this.filesDock.ActiveQueryTab;
            if (editor != null)
                DoSaveAs(editor);
        }

        private bool DoSaveAs(QueryTab editor)
        {
            saveFileDialog.FileName = editor.FileName;
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
                return false;

            return SaveEditor(editor, saveFileDialog.FileName);
        }

        private bool SaveEditor(QueryTab editor, string fileName)
        {
            try
            {
                File.WriteAllText(fileName, editor.QueryText);
                editor.FileName = fileName;
                editor.MarkSaved();

                // The syntax highlighting strategy doesn't change
                // automatically, so do it manually.
                //editor.Document.HighlightingStrategy = 
                //    HighlightingStrategyFactory.CreateHighlightingStrategyForFile(editor.FileName);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, ex.GetType().Name);
                return false;
            }
        }

        private void menuNotificationListenerActive_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                if (SubscriptionManager.IsListening())
                {
                    SubscriptionManager.CloseListeningService();
                    menuNotificationListenerActive.Checked = false;
                }
                else
                {
                    Action x = () => menuNotificationListenerActive.Checked = true;
                    SubscriptionManager.StartListening(() => this.BeginInvoke(x));
                }
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void menuFileExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        #endregion

        #region Code related to Edit menu

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
                this.filesDock.ActiveQueryTab?.Undo();
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
                this.filesDock.ActiveQueryTab?.Redo();
        }

        private void menuEditCut_Click(object sender, EventArgs e)
        {
                this.filesDock.ActiveQueryTab?.Cut();
        }

        private void menuEditCopy_Click(object sender, EventArgs e)
        {
            this.filesDock.ActiveQueryTab?.CopySelectionToClipboard();
        }

        private void menuEditPaste_Click(object sender, EventArgs e)
        {
            this.filesDock.ActiveQueryTab?.Paste();
        }

        #endregion

        #region Other stuff

        private void TextEditorDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(Entity)))
                e.Effect = DragDropEffects.Copy;
        }

        private void TextEditorDragDrop(object sender, DragEventArgs e)
        {
            Entity entity = e.Data.GetData(typeof(Entity)) as Entity;
            if (entity != null)
                this.filesDock.GenerateSelectStatement(entity, (e.KeyState & 8) == 8);
        }

        private void TextEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Ask user to save changes
            foreach (var editor in this.filesDock.AllEditors)
            {
                if (editor.Modified && Settings.Default.PromptToSaveOnClose)
                {
                    var r = MessageBox.Show(this, $"Save changes to {editor.FileName ?? "new file"}?",
                        "Save?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Cancel)
                        e.Cancel = true;
                    else if (r == DialogResult.Yes)
                        if (!DoSave(editor))
                            e.Cancel = true;
                }

                if (editor.Tag is ConnectionInfo info)
                {
                    info.Dispose();
                }
            }
        }

        /// <summary>We handle DragEnter and DragDrop so users can drop files on the editor.</summary>
        private void TextEditorForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void TextEditorForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] list = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (list != null)
                this.filesDock.OpenFiles(list);
        }

        #endregion

        private void menuQueryExecute_Click(object sender, EventArgs e)
        {
            if (this.filesDock.ActiveQueryTab != null)
                this.filesDock.ActiveQueryTab.RunQuery();
        }

        private void parametersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.filesDock.ShowParametersToolbox();
        }

        private void enumEntitiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConnectionInfo connection = this.SelectedConnection;
            if (connection == null)
                return; // should we try to connect?

            string errorMsg;

            try
            {
                EntityClassGraph entityClassGraph = new EntityClassGraph(connection.Connection);

                EntityClassGraphForm form = new EntityClassGraphForm(entityClassGraph);
                form.Show(this);
                return;
            }
            catch (FaultException<InfoServiceFaultContract> ex)
            {
                log.Error("Failed to connect", ex);
                errorMsg = ex.Detail.Message;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
            }

            if (errorMsg != null)
            {
                errorMsg = string.Format("Unable to connect to generate entity graph. {0}", errorMsg);
                MessageBox.Show(this, errorMsg, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void playbackToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (this.filesDock.ActiveQueryTab != null)
                this.filesDock.ActiveQueryTab.RunPlayback();
        }

        private void aboutSWQLStudioToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new About().ShowDialog(this);
        }

        private void byNamespaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetEntityGroupingMode(EntityGroupingMode.ByNamespace);
        }

        private void noGroupingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetEntityGroupingMode(EntityGroupingMode.Flat);
        }

        private void SetEntityGroupingMode(EntityGroupingMode mode)
        {
            byNamespaceToolStripMenuItem.Checked = mode == EntityGroupingMode.ByNamespace;
            noGroupingToolStripMenuItem.Checked = mode == EntityGroupingMode.Flat;
            byBaseTypeToolStripMenuItem.Checked = mode == EntityGroupingMode.ByBaseType;
            byHierarchyToolStripMenuItem.Checked = mode == EntityGroupingMode.ByHierarchy;

            Settings.Default.EntityGroupingMode = mode.ToString();
            Settings.Default.Save();

            this.filesDock.SetEntityGroupingMode(mode);
        }

        private void byBaseTypeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetEntityGroupingMode(EntityGroupingMode.ByBaseType);
        }

        private void byHierarchyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetEntityGroupingMode(EntityGroupingMode.ByHierarchy);
        }

        private void fileToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            menuNotificationListenerActive.Visible =
                separatorAboveNotificationListenerActive.Visible =
                    !Settings.Default.UseActiveSubscriber;

            menuNotificationListenerActive.CheckState = SubscriptionManager.IsListening()
                ? CheckState.Checked
                : CheckState.Unchecked;
        }

        private void menuFileTabPage_Click(object sender, EventArgs e)
        {
            this.filesDock.CreateTabFromPrevious();
        }

        private void enableAutocompleteToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.AutocompleteEnabled = enableAutocompleteToolStripMenuItem.Checked;
            Settings.Default.Save();
        }

        private void preferencesToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            enableAutocompleteToolStripMenuItem.Checked = Settings.Default.AutocompleteEnabled;
            promptToSaveOnCloseToolStripMenuItem.Checked = Settings.Default.PromptToSaveOnClose;
        }

        private void searchInTreeHotKeyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.filesDock.FocusSearch();
        }

        private void discoverQueryParametersToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this.filesDock.AllowSetParameters(this.discoverQueryParametersMenuItem.Checked);
        }

        private void connectionsCombobox_SelectedIndexChanged(object sender, EventArgs e)
        {
            IConnectionTab activeConnectionTab = this.filesDock.ActiveConnectionTab;
            if (activeConnectionTab != null && this.SelectedConnection != null)
            {
                activeConnectionTab.ConnectionInfo = this.SelectedConnection;
            }
        }

        private void newConnectionButton_Click(object sender, EventArgs e)
        {
            this.connectionsManager.CreateConnection();
        }

        private void disconnectToolButton_Click(object sender, EventArgs e)
        {
            var connection = this.connectionsCombobox.SelectedItem as ConnectionInfo;
            if (connection != null)
            {
                this.filesDock.CloseServer(connection);
            }
        }

        private void refreshToolButton_Click(object sender, EventArgs e)
        {
            var connection = this.connectionsCombobox.SelectedItem as ConnectionInfo;
            if (connection != null)
            {
                this.filesDock.RefreshServer(connection);
            }
        }

        private void editToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            copyQueryAsToolStripMenuItem.Enabled = filesDock.ActiveQueryTab != null;
        }

        private void curlCmdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyQueryAs(CommandLineGenerator.GetQueryForCurlCmd);
        }

        private void curlBashToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyQueryAs(CommandLineGenerator.GetQueryForCurlBash);
        }

        private void getSwisDataPowerShellToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyQueryAs(CommandLineGenerator.GetQueryForPowerShellGetSwisData);
        }

        private void CopyQueryAs(Func<string, ConnectionInfo, string> formatter)
        {
            var connection = filesDock.ActiveConnectionTab.ConnectionInfo;
            var query = filesDock.ActiveQueryTab.QueryText;
            string command = formatter(query, connection);
            Clipboard.SetText(command);
        }

        private void promptToSaveOnCloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.PromptToSaveOnClose = !Settings.Default.PromptToSaveOnClose;
            Settings.Default.Save();
        }
    }
}
