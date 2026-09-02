/*
 * Mugman Skin Mod - Importar una secuencia alineada como grupos de Photoshop.
 *
 * 1. Ejecuta Archivo > Secuencias de comandos > Examinar.
 * 2. Selecciona una carpeta dentro de generated/aligned/<accion>/<secuencia>.
 * 3. El script crea un documento con un grupo por frame, tres capas
 *    (Gorra, Barba y Original) y un cuadro de animación por grupo.
 */

#target photoshop

(function () {
    function duplicateCurrentAnimationFrame() {
        var duplicate = charIDToTypeID("Dplc");
        var descriptor = new ActionDescriptor();
        var reference = new ActionReference();
        reference.putEnumerated(
            stringIDToTypeID("animationFrameClass"),
            charIDToTypeID("Ordn"),
            charIDToTypeID("Trgt")
        );
        descriptor.putReference(charIDToTypeID("null"), reference);
        executeAction(duplicate, descriptor, DialogModes.NO);
    }

    function selectAnimationFrame(frameNumber) {
        var descriptor = new ActionDescriptor();
        var reference = new ActionReference();
        reference.putIndex(
            stringIDToTypeID("animationFrameClass"),
            frameNumber
        );
        descriptor.putReference(charIDToTypeID("null"), reference);
        executeAction(charIDToTypeID("slct"), descriptor, DialogModes.NO);
    }

    function createAnimationFromGroups(groups) {
        var index;
        for (index = 0; index < groups.length; index++) {
            groups[index].visible = false;
        }
        groups[0].visible = true;

        executeAction(
            stringIDToTypeID("makeFrameAnimation"),
            undefined,
            DialogModes.NO
        );

        for (index = 1; index < groups.length; index++) {
            duplicateCurrentAnimationFrame();
            groups[index - 1].visible = false;
            groups[index].visible = true;
        }

        selectAnimationFrame(1);
    }

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
    var frameGroups = [];

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
        frameGroups.push(group);
        imported.move(group, ElementPlacement.INSIDE);
        imported.name = "Original (bloqueado)";
        imported.allLocked = true;

        var beard = group.artLayers.add();
        beard.name = "Barba";
        var cap = group.artLayers.add();
        cap.name = "Gorra";
    }

    try {
        createAnimationFromGroups(frameGroups);
        alert(
            "Secuencia importada: " + files.length + " frames.\n\n" +
            "La línea de tiempo ya contiene un cuadro por grupo. No uses " +
            "'Make Frames From Layers', porque separaría también Gorra, " +
            "Barba y Original."
        );
    } catch (error) {
        alert(
            "Los grupos se importaron, pero Photoshop no pudo construir " +
            "automáticamente la línea de tiempo.\n\nDetalle: " + error.message
        );
    }
}());
