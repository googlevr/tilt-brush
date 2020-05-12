// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using UnityEngine;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace TiltBrush {

public class YouTubeLiveChat : BaseChatScript {
  private static string kYouTubeApiKey => App.Config.GoogleSecrets?.ApiKey;

  [SerializeField] private float m_RequestMessagesInterval = 1.0f;
  private float m_RequestMessagesCountdown;

  private string m_ChannelID;
  private string m_VideoId;
  private string m_ActiveLiveChatId;
  private string m_NextPageToken;
  private bool m_OutGatheringMessages;

  private enum ConnectionState {
    Connect_Start,
    OK,
    Failed_NoChannel,
    Failed_NoVideoID,
    Failed_NoChatID,
    Failed_NoMessages,
  }
  private ConnectionState m_ConnectionState;

  void Start() {
    TiltBrush.App.Instance.RefreshUserConfig();
    m_ChannelID = App.UserConfig.YouTube.ChannelID;
    ClearChatLines();

    m_ConnectionState = ConnectionState.Connect_Start;
    if (m_ChannelID == null) {
      ConnectionFailed(ConnectionState.Failed_NoChannel);
    }
  }

  void ConnectionFailed(ConnectionState reason) {
    m_ConnectionState = reason;

    bool bShowConfigEx = true;
    switch (m_ConnectionState) {
    case ConnectionState.Failed_NoChannel:
      AddLine("No Channel ID specified.", "<color=red><i>", "</i></color>");
      break;
    case ConnectionState.Failed_NoVideoID:
      AddLine("Failure to get Video ID.", "<color=red><i>", "</i></color>");
      AddLine("Is the channel live?", "<color=red><i>", "</i></color>");
      break;
    case ConnectionState.Failed_NoChatID:
      AddLine("Failure to get Chat ID.", "<color=red><i>", "</i></color>");
      bShowConfigEx = false;
      break;
    case ConnectionState.Failed_NoMessages:
      AddLine("Failure to get chat messages.", "<color=red><i>", "</i></color>");
      bShowConfigEx = false;
      break;
    }

    if (bShowConfigEx) {
      AddLine("Check config file:");
      AddLine(TiltBrush.App.ConfigPath(), m_Blue, "</color>");
      AddLine("Syntax example:");
      AddLine("{");
      AddLine("  \"YouTube\": {");
      AddLine("    \"ChannelID\": \"abcdefghijklmnopqrstuvwx\",");
      AddLine("  },");
      AddLine("}");
      AddLine("Close and reopen to refresh.");
    }
    RefreshChatText();
  }

  void Update() {
    switch (m_ConnectionState) {
    case ConnectionState.Connect_Start:
      // Get the proper credentials with our Channel ID.
      StartCoroutine(GetActiveLiveChatIDForChannel());
      m_ConnectionState = ConnectionState.OK;
      m_OutGatheringMessages = false;
      break;
    case ConnectionState.OK:
      if (m_ActiveLiveChatId != null && !m_OutGatheringMessages) {
        m_RequestMessagesCountdown -= Time.deltaTime;
        if (m_RequestMessagesCountdown <= 0.0f) {
          StartCoroutine(GetMessageList());
        }
      }
      break;
    case ConnectionState.Failed_NoChannel:
    case ConnectionState.Failed_NoVideoID:
    case ConnectionState.Failed_NoChatID:
    case ConnectionState.Failed_NoMessages:
      break;
    }
  }

  IEnumerator GetActiveLiveChatIDForChannel() {
    // Using our Channel ID, get the Video ID of the currently live broadcast.
    // Then, get the activeLiveChatId from the liveStreamingDetails part.
    m_VideoId = null;
    m_ActiveLiveChatId = null;

    // Using our Channel ID, get the Video ID of the currently live broadcast.
    string sSearchURL = string.Format(
        "https://www.googleapis.com/youtube/v3/search?" +
        "part=snippet&" +
        "channelId={0}&" +
        "eventType=live&" +
        "type=video&" +
        "key={1}",
        m_ChannelID, kYouTubeApiKey);
    var videoIDRequest = UnityWebRequest.Get(sSearchURL);
    videoIDRequest.SetRequestHeader("Content-Type", "application/json");
    videoIDRequest.SetRequestHeader("Accept", "application/json");

    // Request and wait until we get a response.
    yield return videoIDRequest.SendWebRequest();

    // Error checking.
    if (videoIDRequest.error != null) {
      ConnectionFailed(ConnectionState.Failed_NoVideoID);
      yield break;
    }

    // Dig the Video ID out of the response.
    while (!videoIDRequest.downloadHandler.isDone) { yield return null; }
    JObject videoRss = JObject.Parse(videoIDRequest.downloadHandler.text);
    if (videoRss == null) {
      ConnectionFailed(ConnectionState.Failed_NoVideoID);
      yield break;
    }

    if (videoRss["items"] != null) {
      if (videoRss["items"].HasValues && videoRss["items"][0] != null) {
        if (videoRss["items"][0]["id"] != null) {
          m_VideoId = (string)videoRss["items"][0]["id"]["videoId"];
        }
      }
    }

    if (m_VideoId == null) {
      ConnectionFailed(ConnectionState.Failed_NoVideoID);
      yield break;
    }

    // Now, get the activeLiveChatId from the liveStreamingDetails part.
    string sVideosURL = string.Format(
        "https://www.googleapis.com/youtube/v3/videos?" +
        "part=liveStreamingDetails&" +
        "id={0}&" +
        "key={1}",
        m_VideoId, kYouTubeApiKey);
    var chatIDRequest = UnityWebRequest.Get(sVideosURL);
    chatIDRequest.SetRequestHeader("Content-Type", "application/json");
    chatIDRequest.SetRequestHeader("Accept", "application/json");

    // Request and wait until we get a response.
    yield return chatIDRequest.SendWebRequest();

    // Error checking.
    if (chatIDRequest.error != null) {
      ConnectionFailed(ConnectionState.Failed_NoChatID);
      yield break;
    }

    // Dig the activeLiveChatId out of the response.
    while (!chatIDRequest.downloadHandler.isDone) { yield return null; }
    JObject chatRss = JObject.Parse(chatIDRequest.downloadHandler.text);
    if (chatRss == null) {
      ConnectionFailed(ConnectionState.Failed_NoChatID);
      yield break;
    }

    if (chatRss["items"] != null) {
      if (chatRss["items"].HasValues && chatRss["items"][0] != null) {
        if (chatRss["items"][0]["liveStreamingDetails"] != null) {
          m_ActiveLiveChatId = (string)chatRss["items"][0]["liveStreamingDetails"]["activeLiveChatId"];
        }
      }
    }

    if (m_ActiveLiveChatId == null) {
      ConnectionFailed(ConnectionState.Failed_NoChatID);
      yield break;
    }

    AddLine("Connection Successful.", "<color=green>", "</color>");
    RefreshChatText();

    yield return null;
  }

  IEnumerator GetMessageList() {
    if (m_ActiveLiveChatId == null) {
      yield break;
    }

    m_OutGatheringMessages = true;

    // List messages from liveChatMessages.list using the activeLiveChatId.
    string sLiveChatURL = "https://www.googleapis.com/youtube/v3/liveChat/messages?" +
        "part=snippet,authorDetails&";
    if (m_NextPageToken != null) {
      sLiveChatURL = string.Format(sLiveChatURL +
        "liveChatId={0}&" +
        "pageToken={1}&" +
        "key={2}",
        m_ActiveLiveChatId, m_NextPageToken, kYouTubeApiKey);
    } else {
      sLiveChatURL = string.Format(sLiveChatURL +
        "liveChatId={0}&" +
        "key={1}",
        m_ActiveLiveChatId, kYouTubeApiKey);
    }

    var messagesRequest = UnityWebRequest.Get(sLiveChatURL);
    messagesRequest.SetRequestHeader("Content-Type", "application/json");
    messagesRequest.SetRequestHeader("Accept", "application/json");

    // Request and wait until we get a response.
    yield return messagesRequest.SendWebRequest();

    // Error checking.
    if (messagesRequest.error != null) {
      ConnectionFailed(ConnectionState.Failed_NoMessages);
      yield break;
    }

    // Dig the messages out of the response.
    while (!messagesRequest.downloadHandler.isDone) { yield return null; }
    JObject messagesRss = JObject.Parse(messagesRequest.downloadHandler.text);
    if (messagesRss == null) {
      ConnectionFailed(ConnectionState.Failed_NoMessages);
      yield break;
    }

    // Figure out how many new messages we've got.
    int iNumMessages = 0;
    if ((messagesRss["pageInfo"] != null) && (messagesRss["pageInfo"]["totalResults"] != null)) {
      iNumMessages = (int)messagesRss["pageInfo"]["totalResults"];
    }

    // Store the next page token so we only get the freshest hits next time we ask.
    if (messagesRss["nextPageToken"] != null) {
      m_NextPageToken = (string)messagesRss["nextPageToken"];
    }

    // Update again when it thinks we should.
    if (messagesRss["pollingIntervalMillis"] != null) {
      m_RequestMessagesCountdown = (float)messagesRss["pollingIntervalMillis"] / 1000.0f;
    } else {
      m_RequestMessagesCountdown = m_RequestMessagesInterval;
    }

    if (iNumMessages > 0) {
      if (messagesRss["items"] != null && messagesRss["items"].HasValues) {
        for (int i = 0; i < iNumMessages; ++i) {
          if (messagesRss["items"][i] != null) {
            // Get author of each message.
            string sAuthor = "";
            if ((messagesRss["items"][i]["authorDetails"] != null) &&
                (messagesRss["items"][i]["authorDetails"]["displayName"] != null)) {
              sAuthor = (string)messagesRss["items"][i]["authorDetails"]["displayName"];
            }

            // Get message and add it to the list.
            if ((messagesRss["items"][i]["snippet"] != null) &&
                (messagesRss["items"][i]["snippet"]["displayMessage"] != null)) {
              AddLine(sAuthor + ": " + (string)messagesRss["items"][i]["snippet"]["displayMessage"]);
            }
          }
        }
      }

      // Don't bother refreshing if nothing's changed.
      RefreshChatText();
    }

    m_OutGatheringMessages = false;

    yield return null;
  }
}

}