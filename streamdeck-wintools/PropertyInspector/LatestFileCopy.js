function openSaveFilePicker(title, filter, propertyName) {
    console.log("openSaveFilePicker called: ", title, filter, propertyName);
    var payload = {};
    payload.property_inspector = 'loadsavepicker';
    payload.picker_title = title;
    payload.picker_filter = filter;
    payload.property_name = propertyName;
    sendPayloadToPlugin(payload);
}

function openDirectoryPicker(title, propertyName) {
    console.log("openDirectoryPicker called: ", title, propertyName);
    var payload = {};
    payload.property_inspector = 'loadfolderpicker';
    payload.picker_title = title;
    payload.property_name = propertyName;
    sendPayloadToPlugin(payload);
}