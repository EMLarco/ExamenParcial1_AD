(function () {
    "use strict";

    var results = document.getElementById("contentResults");
    var status = document.getElementById("realtime-status");
    var alertBox = document.getElementById("realtime-alert");

    if (!results || typeof signalR === "undefined") {
        return;
    }

    var resultsUrl = results.dataset.resultsUrl;
    var refreshTimer;

    function showStatus(text) {
        if (status) {
            status.textContent = text;
        }
    }

    function showAlert(message) {
        if (!alertBox) {
            return;
        }

        alertBox.textContent = message;
        alertBox.classList.remove("d-none");
        window.clearTimeout(alertBox.hideTimer);
        alertBox.hideTimer = window.setTimeout(function () {
            alertBox.classList.add("d-none");
        }, 5000);
    }

    function refreshResults() {
        fetch(resultsUrl + window.location.search, {
            headers: { "X-Requested-With": "XMLHttpRequest" }
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("No se pudo actualizar el listado.");
                }
                return response.text();
            })
            .then(function (html) {
                results.innerHTML = html;
            })
            .catch(function () {
                showAlert("No se pudo actualizar el listado automáticamente.");
            });
    }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/contentHub")
        .withAutomaticReconnect()
        .build();

    connection.on("ContentChanged", function (data) {
        if (data && data.message) {
            showAlert(data.message);
        }

        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(refreshResults, 300);
    });

    connection.onreconnecting(function () {
        showStatus("Reconectando al canal en tiempo real...");
    });

    connection.onreconnected(function () {
        showStatus("Conectado. Los cambios se actualizan automáticamente.");
        refreshResults();
    });

    connection.onclose(function () {
        showStatus("Canal en tiempo real desconectado.");
    });

    connection.start()
        .then(function () {
            showStatus("Conectado. Los cambios se actualizan automáticamente.");
        })
        .catch(function () {
            showStatus("No se pudo conectar al canal en tiempo real.");
        });
})();
