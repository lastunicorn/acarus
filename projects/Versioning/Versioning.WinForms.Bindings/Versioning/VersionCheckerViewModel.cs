﻿// Acarus
// Copyright (C) 2015 Dust in the Wind
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using System.Net;
using System.Windows.Forms;
using DustInTheWind.Versioning.WinForms.Mvp.Common;
using DustInTheWind.Versioning.WinForms.Mvp.Properties;

namespace DustInTheWind.Versioning.WinForms.Mvp.Versioning
{
    /// <summary>
    /// ViewModel class that contains the logic of the Version Checker window.
    /// </summary>
    public class VersionCheckerViewModel : ViewModelBase
    {
        private int progressBarValue;
        private bool progressBarVisible;
        private bool downloadButtonVisible;
        private bool openDownloadedFileButtonVisible;
        private bool checkAgainButtonEnabled;
        private string statusText;
        private string informationText;
        private bool checkAtStartupEnabled;
        private bool checkAtStartupValue;
        private ProgressBarStyle progressBarStyle;

        /// <summary>
        /// Gets or sets the view that represents the GUI.
        /// </summary>
        public IVersionCheckerView View { get; set; }

        /// <summary>
        /// The default url used if the configuration does not specify another one.
        /// </summary>
        public string AppWebPage { get; set; }

        /// <summary>
        /// Provides configuration values to be used in Azzul application.
        /// </summary>
        private readonly IOptions options;

        /// <summary>
        /// A service that checks if a newer version of Azzul exists.
        /// </summary>
        private readonly VersionChecker versionChecker;

        /// <summary>
        /// A helper class used to download files.
        /// </summary>
        private readonly FileDownloader fileDownloader;

        /// <summary>
        /// A service that displays messages to the user.
        /// </summary>
        private readonly IUserInterface userInterface;

        public int ProgressBarValue
        {
            get { return progressBarValue; }
            private set
            {
                progressBarValue = value;
                OnPropertyChanged();
            }
        }

        public bool ProgressBarVisible
        {
            get { return progressBarVisible; }
            private set
            {
                progressBarVisible = value;
                OnPropertyChanged();
            }
        }

        public bool DownloadButtonVisible
        {
            get { return downloadButtonVisible; }
            private set
            {
                downloadButtonVisible = value;
                OnPropertyChanged();
            }
        }

        public bool OpenDownloadedFileButtonVisible
        {
            get { return openDownloadedFileButtonVisible; }
            private set
            {
                openDownloadedFileButtonVisible = value;
                OnPropertyChanged();
            }
        }

        public bool CheckAgainButtonEnabled
        {
            get { return checkAgainButtonEnabled; }
            private set
            {
                checkAgainButtonEnabled = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get { return statusText; }
            private set
            {
                statusText = value;
                OnPropertyChanged();
            }
        }

        public string InformationText
        {
            get { return informationText; }
            private set
            {
                informationText = value;
                OnPropertyChanged();
            }
        }

        public bool CheckAtStartupEnabled
        {
            get { return checkAtStartupEnabled; }
            private set
            {
                checkAtStartupEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool CheckAtStartupValue
        {
            get { return checkAtStartupValue; }
            set
            {
                checkAtStartupValue = value;
                OnPropertyChanged();
            }
        }

        public ProgressBarStyle ProgressBarStyle
        {
            get { return progressBarStyle; }
            set
            {
                progressBarStyle = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="VersionCheckerViewModel"/> class.
        /// </summary>
        /// <param name="versionChecker">A service that checks if a newer version of the application exists.</param>
        /// <param name="userInterface">A service that displays messages to the user.</param>
        /// <param name="options">Provides configuration values to be used in the application.</param>
        /// <exception cref="ArgumentNullException">Exception thrown if one of the arguments is null.</exception>
        public VersionCheckerViewModel(VersionChecker versionChecker, IUserInterface userInterface, IOptions options)
        {
            if (versionChecker == null) throw new ArgumentNullException("versionChecker");
            if (userInterface == null) throw new ArgumentNullException("userInterface");
            if (options == null) throw new ArgumentNullException("options");

            this.versionChecker = versionChecker;
            this.userInterface = userInterface;
            this.options = options;

            ChangeStateToEmpty();

            versionChecker.CheckStarting += HandleVersionCheckStarting;
            versionChecker.CheckCompleted += HandleCheckCompleted;

            fileDownloader = new FileDownloader(userInterface);
            fileDownloader.DownloadFileStarting += HandleDownloadFileStarting;
            fileDownloader.DownloadProgressChanged += HandleDownloadProgressChanged;
            fileDownloader.DownloadFileCompleted += HandleDownloadFileCompleted;

            CheckAtStartupEnabled = true;
            options.CheckAtStartupChanged += HandleOptionsCheckAtStartupChanged;
            CheckAtStartupValue = options.CheckAtStartup;
        }

        private void HandleOptionsCheckAtStartupChanged(object sender, EventArgs eventArgs)
        {
            CheckAtStartupValue = options.CheckAtStartup;
        }

        private void HandleVersionCheckStarting(object sender, EventArgs eventArgs)
        {
            ChangeStateToBeginVersionCheck();
        }

        private void HandleCheckCompleted(object sender, CheckCompletedEventArgs e)
        {
            userInterface.Dispatch(() =>
            {
                try
                {
                    if (e.Error != null)
                    {
                        ChangeStateToVersionCheckError(e.Error);
                    }
                    else
                    {
                        VersionCheckingResult checkingResult = versionChecker.LastCheckingResult;

                        if (checkingResult.IsNewerVersion)
                            ChangeStateToShowNewVersion(checkingResult);
                        else
                            ChangeStateToShowNoVersion();
                    }
                }
                catch (Exception ex)
                {
                    userInterface.DisplayError(ex);
                }
            });
        }

        private void HandleDownloadFileStarting(object sender, EventArgs e)
        {
            userInterface.Dispatch(ChangeStateToBeginDownload);
        }

        private void HandleDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            userInterface.Dispatch(() =>
            {
                ProgressBarValue = e.ProgressPercentage;

                string applicationName = versionChecker.LastCheckingResult.RetrievedAppVersionInfo.Name;
                string informationalVersion = versionChecker.LastCheckingResult.RetrievedAppVersionInfo.InformationalVersion;
                long kiloBytesReceived = e.BytesReceived / 1024;
                long totalKiloBytesToReceive = e.TotalBytesToReceive / 1024;

                InformationText = string.Format(VersionCheckerResources.VersionCheckerWindow_Downloading, applicationName, informationalVersion, kiloBytesReceived, totalKiloBytesToReceive);
            });
        }

        private void HandleDownloadFileCompleted(object sender, DownloadFileCompletedEventArgs e)
        {
            userInterface.Dispatch(() =>
            {
                if (e.Cancelled)
                {
                    ChangeStateToDownloadCanceled();
                }
                else
                {
                    if (e.Error != null)
                        ChangeStateToDownloadError(e.Error);
                    else
                        ChangeStateToDownloadSuccess();
                }
            });
        }

        public void ViewShown()
        {
            try
            {
                VersionCheckingResult lastCheckingResult = versionChecker.LastCheckingResult;

                if (lastCheckingResult == null)
                {
                    versionChecker.CheckAsync();
                }
                else
                {
                    if (lastCheckingResult.IsNewerVersion)
                        ChangeStateToShowNewVersion(lastCheckingResult);
                    else
                        ChangeStateToShowNoVersion();
                }
            }
            catch (Exception ex)
            {
                userInterface.DisplayError(ex);
            }
        }

        public void CloseButtonClicked()
        {
            try
            {
                versionChecker.Stop();
                fileDownloader.Stop();

                View.CloseView();
            }
            catch (Exception ex)
            {
                userInterface.DisplayError(ex);
            }
        }

        public void CheckAgainButtonClicked()
        {
            try
            {
                versionChecker.CheckAsync();
            }
            catch (Exception ex)
            {
                userInterface.DisplayError(ex);
            }
        }

        public void DownloadButtonClicked()
        {
            try
            {
                if (versionChecker.LastCheckingResult == null ||
                    versionChecker.LastCheckingResult.RetrievedAppVersionInfo == null ||
                    versionChecker.LastCheckingResult.RetrievedAppVersionInfo.DownloadUrl == null)
                    return;

                string downloadUrl = versionChecker.LastCheckingResult.RetrievedAppVersionInfo.DownloadUrl;

                fileDownloader.Download(downloadUrl);
            }
            catch (Exception ex)
            {
                userInterface.DisplayError(ex);
            }
        }

        public void CheckAtStartupCheckedChanged()
        {
            try
            {
                options.CheckAtStartup = CheckAtStartupValue;
            }
            catch (Exception ex)
            {
                userInterface.DisplayError(ex);
            }
        }

        public void OpenDownloadedFileButtonClicked()
        {
            try
            {
                Process.Start(fileDownloader.DownloadedFilePath);
            }
            catch (Exception ex)
            {
                userInterface.DisplayError(ex);
            }
        }

        private void ChangeStateToEmpty()
        {
            ProgressBarVisible = false;

            StatusText = string.Empty;
            InformationText = string.Empty;

            DownloadButtonVisible = false;
            CheckAgainButtonEnabled = true;
        }

        private void ChangeStateToBeginVersionCheck()
        {
            ProgressBarVisible = true;
            ProgressBarStyle = ProgressBarStyle.Marquee;

            string location = versionChecker.AppInfoProvider == null
                ? null
                : versionChecker.AppInfoProvider.Location;

            InformationText = string.Format(VersionCheckerResources.VersionCheckerWindow_Checking, location);
            StatusText = VersionCheckerResources.VersionCheckerWindow_StatusText_Checking;

            OpenDownloadedFileButtonVisible = false;
            DownloadButtonVisible = false;
            CheckAgainButtonEnabled = false;
        }

        private void ChangeStateToShowNoVersion()
        {
            ProgressBarVisible = false;

            StatusText = VersionCheckerResources.VersionCheckerWindow_StatusText_NoNewVersion;
            InformationText = VersionCheckerResources.VersionCheckerWindow_NoNewVersion;

            DownloadButtonVisible = false;
            CheckAgainButtonEnabled = true;
        }

        private void ChangeStateToShowNewVersion(VersionCheckingResult versionCheckingResult)
        {
            ProgressBarVisible = false;

            AppVersionInfo versionInformation = versionCheckingResult.RetrievedAppVersionInfo;

            StatusText = string.Format(VersionCheckerResources.VersionCheckerWindow_StatusText_NewVersionAvailable, versionInformation.InformationalVersion);
            InformationText = versionInformation.DownloadUrl != null
                ? string.Format(VersionCheckerResources.VersionCheckerWindow_VersionDescription, versionInformation.Description)
                : string.Format(VersionCheckerResources.VersionCheckerWindow_VersionDescriptionNoDownload, versionInformation.Description, AppWebPage);

            DownloadButtonVisible = versionInformation.DownloadUrl != null;
            CheckAgainButtonEnabled = true;
        }

        private void ChangeStateToVersionCheckError(Exception exception)
        {
            ProgressBarVisible = false;

            StatusText = VersionCheckerResources.VersionCheckerWindow_StatusText_ErrorChecking;
            InformationText = exception.Message;

            DownloadButtonVisible = false;
            CheckAgainButtonEnabled = true;
        }

        private void ChangeStateToBeginDownload()
        {
            ProgressBarVisible = true;
            ProgressBarStyle = ProgressBarStyle.Blocks;
            ProgressBarValue = 0;

            string appName = versionChecker.LastCheckingResult.RetrievedAppVersionInfo.Name;
            string appVersion = versionChecker.LastCheckingResult.RetrievedAppVersionInfo.InformationalVersion;
            InformationText = string.Format(VersionCheckerResources.VersionCheckerWindow_Downloading, appName, appVersion, 0, 0);

            DownloadButtonVisible = false;
            CheckAgainButtonEnabled = false;
        }

        private void ChangeStateToDownloadSuccess()
        {
            ProgressBarVisible = false;

            InformationText = string.Format(VersionCheckerResources.VersionCheckerWindow_DownloadSuccess, fileDownloader.DownloadedFilePath);

            OpenDownloadedFileButtonVisible = true;
            DownloadButtonVisible = false;
            CheckAgainButtonEnabled = true;
        }

        private void ChangeStateToDownloadCanceled()
        {
            ProgressBarVisible = false;

            InformationText = VersionCheckerResources.VersionCheckerWindow_DownloadCanceled;

            DownloadButtonVisible = false;
            CheckAgainButtonEnabled = true;
        }

        private void ChangeStateToDownloadError(Exception ex)
        {
            ProgressBarVisible = false;

            StatusText = VersionCheckerResources.VersionCheckerWindow_StatusText_DownloadError;
            InformationText = ex.Message;

            DownloadButtonVisible = true;
            CheckAgainButtonEnabled = true;
        }
    }
}