﻿$(document).ready(downloadFile);

function downloadFile() {
    var fd = new FormData();
    fd.append('file', fileName);
    fd.append('__RequestVerificationToken', $('#__AjaxAntiForgeryForm input[name=__RequestVerificationToken]').val());

    var xhr = new XMLHttpRequest();
    xhr.open('POST', downloadDataUrl, true);
    xhr.responseType = 'arraybuffer';

    xhr.onload = function (e) {
        if (this.status == 200) {
            var worker = new Worker(encScriptSrc);

            worker.addEventListener('message', function (e) {
                switch (e.data.cmd) {
                    case 'progress':
                        var percentComplete = Math.round(e.data.processed * 100 / e.data.total);
                        $("#progress").children('.progress-bar').css('width', (percentComplete / 2) + 50 + '%');
                        $("#progress").children('.progress-bar').html(percentComplete + '% Decrypted');
                        break;
                    case 'finish':
                        $('#progress').children('.progress-bar').css('width', '100%');
                        $('#progress').children('.progress-bar').html('Complete');
                        if (fileType == null || fileType == '')
                        {
                            fileType = "application/octet-stream";
                        }
                        var blob = new Blob([e.data.buffer], { type: fileType });
                        
                        saveAs(blob, fileName);

                        // DO SOMETHING
                        break;
                }
            });

            worker.onerror = function (err) {
                // An error occured
                $("#progress").children('.progress-bar').css('width', '100%');
                $("#progress").children('.progress-bar').removeClass('progress-bar-success');
                $("#progress").children('.progress-bar').addClass('progress-bar-danger');
                $("#progress").children('.progress-bar').html('Error Occured');
            }

            // Execute worker with data
            var objData =
                {
                    cmd: 'decrypt',
                    script: aesScriptSrc,
                    key: key,
                    iv: iv,
                    chunkSize: chunkSize,
                    file: this.response
                };
            worker.postMessage(objData, [objData.file]);
        }
    };

    xhr.onprogress = function (e) {
        if (e.lengthComputable) {
            var percentComplete = Math.round(e.loaded * 100 / e.total);
            $('#progress').children('.progress-bar').css('width', (percentComplete / 2) + '%');
            $('#progress').children('.progress-bar').html(percentComplete + '% Downloaded');
        }
    };

    xhr.onerror = function (e) {
        $('#progress').children('.progress-bar').css('width', '100%');
        $('#progress').children('.progress-bar').removeClass('progress-bar-success');
        $('#progress').children('.progress-bar').addClass('progress-bar-danger');
        $('#progress').children('.progress-bar').html('Download Failed');
    };

    xhr.onabort = function (e) {
        $('#progress').children('.progress-bar').css('width', '100%');
        $('#progress').children('.progress-bar').removeClass('progress-bar-success');
        $('#progress').children('.progress-bar').addClass('progress-bar-warning');
        $('#progress').children('.progress-bar').html('Download Failed');
    };

    xhr.send(fd);
}