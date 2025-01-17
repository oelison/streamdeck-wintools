﻿document.addEventListener('websocketCreate', function () {
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
    setSoundOnSetSettings("none");
    
    if (payload['playSoundOnSet']) {
        setSoundOnSetSettings("");
    }
}

function setSoundOnSetSettings(displayValue) {
    var dvSoundOnSetSettings = document.getElementById('dvSoundOnSetSettings');
    dvSoundOnSetSettings.style.display = displayValue;
}
