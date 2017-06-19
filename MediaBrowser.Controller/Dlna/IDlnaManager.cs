﻿using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Dlna;
using System.Collections.Generic;

namespace MediaBrowser.Controller.Dlna
{
    public interface IDlnaManager
    {
        /// <summary>
        /// Gets the profile infos.
        /// </summary>
        /// <returns>IEnumerable{DeviceProfileInfo}.</returns>
        IEnumerable<DeviceProfileInfo> GetProfileInfos();

        /// <summary>
        /// Gets the profile.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <returns>DeviceProfile.</returns>
        DeviceProfile GetProfile(IDictionary<string,string> headers);

        /// <summary>
        /// Gets the default profile.
        /// </summary>
        /// <returns>DeviceProfile.</returns>
        DeviceProfile GetDefaultProfile();

        /// <summary>
        /// Creates the profile.
        /// </summary>
        /// <param name="profile">The profile.</param>
        void CreateProfile(DeviceProfile profile);
        
        /// <summary>
        /// Updates the profile.
        /// </summary>
        /// <param name="profile">The profile.</param>
        void UpdateProfile(DeviceProfile profile);
        
        /// <summary>
        /// Deletes the profile.
        /// </summary>
        /// <param name="id">The identifier.</param>
        void DeleteProfile(string id);
        
        /// <summary>
        /// Gets the profile.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <returns>DeviceProfile.</returns>
        DeviceProfile GetProfile(string id);
        
        /// <summary>
        /// Gets the profile.
        /// </summary>
        /// <param name="deviceInfo">The device information.</param>
        /// <returns>DeviceProfile.</returns>
        DeviceProfile GetProfile(DeviceIdentification deviceInfo);

        /// <summary>
        /// Gets the server description XML.
        /// </summary>
        /// <param name="headers">The headers.</param>
        /// <param name="serverUuId">The server uu identifier.</param>
        /// <param name="serverAddress">The server address.</param>
        /// <returns>System.String.</returns>
        string GetServerDescriptionXml(IDictionary<string, string> headers, string serverUuId, string serverAddress);

        /// <summary>
        /// Gets the icon.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <returns>DlnaIconResponse.</returns>
        ImageStream GetIcon(string filename);
    }
}
