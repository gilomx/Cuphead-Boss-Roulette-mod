/*
 * Mugman Skin Mod - Importar una secuencia alineada como grupos de Photoshop.
 *
 * 1. Ejecuta Archivo > Secuencias de comandos > Examinar.
 * 2. Selecciona una carpeta dentro de generated/aligned/<accion>/<secuencia>.
 * 3. El script crea un documento con un grupo por frame y tres capas:
 *    Gorra, Barba y Original.
 */

#target photoshop

(function () {
    var folder = Folder.selectDialog("Selecciona una secuencia alineada de Mugman");
    if (!folder) {
        return;
    }

    var files = folder.getFiles(function (file) {
        return file instanceof File && /\.png$/i.test(file.name);
    });
    function naturalParts(value) {
        return value.toLowerCase().split(/(\d+)/);
    }

    files.sort(function (a, b) {
        var left = naturalParts(a.name);
        var right = naturalParts(b.name);
        var length = Math.max(left.length, right.length);
        for (var index = 0; index < length; index++) {
            var leftPart = left[index] || "";
            var rightPart = right[index] || "";
            var leftNumber = /^\d+$/.test(leftPart) ? parseInt(leftPart, 10) : NaN;
            var rightNumber = /^\d+$/.test(rightPart) ? parseInt(rightPart, 10) : NaN;
            if (!isNaN(leftNumber) && !isNaN(rightNumber)) {
                if (leftNumber !== rightNumber) {
                    return leftNumber - rightNumber;
                }
            } else if (leftPart !== rightPart) {
                return leftPart < rightPart ? -1 : 1;
            }
        }
        return 0;
    });

    if (!files.length) {
        alert("La carpeta no contiene archivos PNG.");
        return;
    }

    var first = app.open(files[0]);
    var width = first.width;
    var height = first.height;
    var resolution = first.resolution;
    first.close(SaveOptions.DONOTSAVECHANGES);

    var target = app.documents.add(
        width,
        height,
        resolution,
        folder.name,
        NewDocumentMode.RGB,
        DocumentFill.TRANSPARENT
    );

    for (var i = 0; i < files.length; i++) {
        var source = app.open(files[i]);
        source.activeLayer.name = "Original";
        var imported = source.activeLayer.duplicate(
            target,
            ElementPlacement.PLACEATBEGINNING
        );
        source.close(SaveOptions.DONOTSAVECHANGES);

        app.activeDocument = target;
        var group = target.layerSets.add();
        group.name = decodeURI(files[i].name).replace(/\.png$/i, "");
        imported.move(group, ElementPlacement.INSIDE);
        imported.name = "Original (bloqueado)";
        imported.allLocked = true;

        var beard = group.artLayers.add();
        beard.name = "Barba";
        var cap = group.artLayers.add();
        cap.name = "Gorra";
    }

    alert(
        "Secuencia importada: " + files.length + " frames.\n\n" +
        "Cada grupo conserva el nombre original. Usa la línea de tiempo de " +
        "Photoshop y 'Crear cuadros a partir de capas'."
    );
}());
