(function () {
  'use strict';

  // Call the initialize API first
  microsoftTeams.initialize();

  // Check the initial theme user chose and respect it
  microsoftTeams.getContext(function (context) {
    if (context && context.theme) {
      setTheme(context.theme);
    }
  });

  // Handle theme changes
  microsoftTeams.registerOnThemeChangeHandler(function (theme) {
    setTheme(theme);
  });

  // Save configuration changes
  microsoftTeams.settings.registerOnSaveHandler(function (saveEvent) {
    // Let the Microsoft Teams platform know what you want to load based on
    // what the user configured on this page
    microsoftTeams.settings.setSettings({
      contentUrl: createTabUrl(), // Mandatory parameter
      entityId: createTabUrl() // Mandatory parameter
    });

    // Tells Microsoft Teams platform that we are done saving our settings. Microsoft Teams waits
    // for the app to call this API before it dismisses the dialog. If the wait times out, you will
    // see an error indicating that the configuration settings could not be saved.
    saveEvent.notifySuccess();
  });

  // Logic to let the user configure what they want to see in the tab being loaded
  document.addEventListener('DOMContentLoaded', function () {
    var tabChoice = document.getElementById('tabChoice');
    if (tabChoice) {
      tabChoice.onchange = function () {
        var selectedTab = this[this.selectedIndex].value;

        // This API tells Microsoft Teams to enable the 'Save' button. Since Microsoft Teams always assumes
        // an initial invalid state, without this call the 'Save' button will never be enabled.
        //microsoftTeams.settings.setValidityState(selectedTab === 'first' || selectedTab === 'second');
          microsoftTeams.settings.setValidityState(true);
      };
    }
      microsoftTeams.settings.setValidityState(true);
      //microsoftTeams.settings.setValidityState(true);
  });

  // Set the desired theme
  function setTheme(theme) {
    if (theme) {
      // Possible values for theme: 'default', 'light', 'dark' and 'contrast'
      document.body.className = 'theme-' + (theme === 'default' ? 'light' : theme);
    }
  }

  // Create the URL that Microsoft Teams will load in the tab. You can compose any URL even with query strings.
  function createTabUrl() {
    var tabChoice = document.getElementById('tabChoice');
    var selectedTab = tabChoice[tabChoice.selectedIndex].value;

      var urlParams = new URLSearchParams(window.location.search);
      var useRSC = true;
      if (urlParams.has('useRSC'))
          useRSC = urlParams.get('useRSC');
      // Teams will substitute the values in {}
      var outputParams = "useRSC=" + useRSC + "&tenantId={tid}&teamId={groupId}&channelId={channelId}";
      return window.location.protocol + '//' + window.location.host + '/first?' + outputParams;
  }
})();


function showLogin(useRSC) {
    var authPath = useRSC ? "/AuthRSC" : "/AuthNoRSC";
    microsoftTeams.authentication.authenticate({
        url: window.location.origin + authPath,
        width: 600,
        height: 535,
        successCallback: function (result) {
            window.location.reload(true);
            getUserProfile(result.accessToken);
        },
        failureCallback: function (reason) {
            // Its landing here even on success
            window.location.reload(true);
            handleAuthError(reason);
        }
    });
}

function loginUsingTabSSO(useRSC) {
    var authTokenRequest = {
        successCallback: function (tabSsoToken) {
            console.log("Tab SSO succeeded: " + tabSsoToken);
            $.ajax({
                url: window.location.origin + "/tokenLogin?useRSC=" + (useRSC ? "true" : "false"),
                method: "POST",
                data: {
                    token: tabSsoToken
                },
                success: function () {
                    console.log("Successfully logged in using SSO token");
                    window.location.reload(true);
                },
                error: function (xhr, textStatus, errorThrown) {
                    let aadError = JSON.parse(xhr.responseText);
                    console.log(aadError);
                    $("#loginButton").text("Authorize access to Microsoft Graph");
                    $("#loginButton").show();
                },
            });
        },
        failureCallback: function (error) {
            console.log("Tab SSO failed: " + error);
            $("#loginButton").show();
        },
    };
    microsoftTeams.authentication.getAuthToken(authTokenRequest);
} 
