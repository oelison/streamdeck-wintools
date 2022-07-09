document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    showHideSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            showHideSettings(payload.settings);
        }
    });
});

function showHideSettings(payload) {
    console.log("Show Hide Settings Called");
    setfadeSettings("none");
    setApplicationChooser("none");

    if (payload['fadeVolume']) {
        setfadeSettings("");
    }

    if (payload['appSpecific']) {
        setApplicationChooser("");
    }
}

function setfadeSettings(displayValue) {
    var dvFadeSettings = document.getElementById('dvFadeSettings');
    dvFadeSettings.style.display = displayValue;
}

function setApplicationChooser(displayValue) {
    var dvAppSpecific = document.getElementById('dvAppSpecific');
    dvAppSpecific.style.display = displayValue;
}

function refreshApplications() {
    var payload = {};
    payload.property_inspector = 'refreshApplications';
    sendPayloadToPlugin(payload);
}