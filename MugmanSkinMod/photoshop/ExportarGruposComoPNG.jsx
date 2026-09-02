/* Exporta cada grupo superior como un PNG de canvas completo. */

#target photoshop

(function () {
    if (!app.documents.length) {
        alert("Abre primero un PSD de una secuencia de Mugman.");
        return;
    }

    var source = app.activeDocument;
    var destination = Folder.selectDialog("Selecciona la carpeta de exportación");
    if (!destination) {
        return;
    }

    var groups = source.layerSets;
    if (!groups.length) {
        alert("El documento no contiene grupos de frames.");
        return;
    }

    for (var i = 0; i < groups.length; i++) {
        for (var j = 0; j < groups.length; j++) {
            groups[j].visible = (i === j);
        }

        var copy = source.duplicate();
        copy.flatten();
        var options = new PNGSaveOptions();
        options.compression = 9;
        options.interlaced = false;
        var file = new File(destination.fsName + "/" + groups[i].name + ".png");
        copy.saveAs(file, options, true, Extension.LOWERCASE);
        copy.close(SaveOptions.DONOTSAVECHANGES);
    }

    for (var k = 0; k < groups.length; k++) {
        groups[k].visible = true;
    }
    alert("Exportados " + groups.length + " frames PNG.");
}());
