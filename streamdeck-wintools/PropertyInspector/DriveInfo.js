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
    setSingleDriveSettings("");
    setMultipleDrivesSettings("none");
    
    if (payload['displayMode'] == 1) {
        setSingleDriveSettings("none");
        setMultipleDrivesSettings("");
    }
}

function setSingleDriveSettings(displayValue) {
    var dvSingleDriveSettings = document.getElementById('dvSingleDriveSettings');
    dvSingleDriveSettings.style.display = displayValue;
}

function setMultipleDrivesSettings(displayValue) {
    var dvMultipleDrivesSettings = document.getElementById('dvMultipleDrivesSettings');
    dvMultipleDrivesSettings.style.display = displayValue;
}
