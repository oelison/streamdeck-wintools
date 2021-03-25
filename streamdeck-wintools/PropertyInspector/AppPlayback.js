document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    showHideSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'sendToPropertyInspector') {
            var payload = jsonObj.payload;
            showHideSettings(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            showHideSettings(payload.settings);
        }
    });
});

function showHideSettings(payload) {
    console.log("Show Hide Settings Called");
    setApplicationChooser("none");

    if (payload['appSpecific']) {
        setApplicationChooser("");
    }
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