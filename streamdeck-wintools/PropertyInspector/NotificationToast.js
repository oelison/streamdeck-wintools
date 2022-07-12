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
    showHideMessageFile("none");
    showHideMessageText("");
    
    if (payload['messageFromFile']) {
        showHideMessageFile("");
        showHideMessageText("none");
    }
}

function showHideMessageFile(displayValue) {
    var dvMessageFileArea = document.getElementById('dvMessageFileArea');
    dvMessageFileArea.style.display = displayValue;
}

function showHideMessageText(displayValue) {
    var dvMessageTextArea = document.getElementById('dvMessageTextArea');
    dvMessageTextArea.style.display = displayValue;
}
