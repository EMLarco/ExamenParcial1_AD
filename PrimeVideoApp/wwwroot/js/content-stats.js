(function () {
    "use strict";

    var app = document.getElementById("statsApp");
    if (!app) {
        return;
    }

    var button = document.getElementById("startStatsButton");
    var statusBox = document.getElementById("statsStatus");
    var message = document.getElementById("statsMessage");
    var progressBar = document.getElementById("statsProgressBar");
    var threadInfo = document.getElementById("statsThread");
    var errorBox = document.getElementById("statsError");
    var resultsBox = document.getElementById("statsResults");
    var startUrl = app.dataset.startUrl;
    var statusUrl = app.dataset.statusUrl;
    var timer;

    function setProgress(progress) {
        var value = Math.max(0, Math.min(100, progress));
        progressBar.style.width = value + "%";
        progressBar.textContent = value + "%";
    }

    function showError(text) {
        errorBox.textContent = text;
        errorBox.classList.remove("d-none");
        button.disabled = false;
    }

    function createCard(title, value) {
        var column = document.createElement("div");
        column.className = "col-md-3";

        var card = document.createElement("div");
        card.className = "card h-100";

        var body = document.createElement("div");
        body.className = "card-body";

        var heading = document.createElement("h5");
        heading.className = "card-title";
        heading.textContent = title;

        var number = document.createElement("p");
        number.className = "card-text fs-3 mb-0";
        number.textContent = value;

        body.appendChild(heading);
        body.appendChild(number);
        card.appendChild(body);
        column.appendChild(card);
        return column;
    }

    function createTable(title, headers, rows) {
        var wrapper = document.createElement("div");
        wrapper.className = "mt-4";

        var heading = document.createElement("h2");
        heading.className = "h4";
        heading.textContent = title;

        var table = document.createElement("table");
        table.className = "table table-striped table-bordered";

        var head = document.createElement("thead");
        var headRow = document.createElement("tr");
        headers.forEach(function (text) {
            var cell = document.createElement("th");
            cell.textContent = text;
            headRow.appendChild(cell);
        });
        head.appendChild(headRow);
        table.appendChild(head);

        var body = document.createElement("tbody");
        rows.forEach(function (row) {
            var tableRow = document.createElement("tr");
            row.forEach(function (value) {
                var cell = document.createElement("td");
                cell.textContent = value;
                tableRow.appendChild(cell);
            });
            body.appendChild(tableRow);
        });
        table.appendChild(body);

        wrapper.appendChild(heading);
        wrapper.appendChild(table);
        return wrapper;
    }

    function renderResults(result, requestThreadId, workerThreadId, elapsedMilliseconds) {
        resultsBox.innerHTML = "";

        var summary = document.createElement("div");
        summary.className = "row g-3";
        summary.appendChild(createCard("Total de contenidos", result.totalContent));
        summary.appendChild(createCard("Películas", result.movies));
        summary.appendChild(createCard("Series", result.tvShows));
        summary.appendChild(createCard("Otros", result.others));

        resultsBox.appendChild(summary);

        var threadMessage = document.createElement("p");
        threadMessage.className = "text-muted small mt-3";
        threadMessage.textContent =
            "Hilo que atendió la solicitud HTTP: #" + requestThreadId +
            " | Hilo del worker: #" + workerThreadId +
            " | Tiempo de procesamiento: " + elapsedMilliseconds + " ms";
        resultsBox.appendChild(threadMessage);

        resultsBox.appendChild(createTable(
            "Contenidos por género",
            ["Género", "Cantidad"],
            result.topGenres.map(function (item) {
                return [item.name, item.count];
            })));

        resultsBox.appendChild(createTable(
            "Contenidos por década",
            ["Década", "Cantidad"],
            result.byDecade.map(function (item) {
                return [item.decade + "s", item.count];
            })));
    }

    function poll(jobId, requestThreadId) {
        fetch(statusUrl + "?id=" + encodeURIComponent(jobId))
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("No se pudo consultar el estado del procesamiento.");
                }
                return response.json();
            })
            .then(function (state) {
                setProgress(state.progress);
                message.textContent = state.status === "processing"
                    ? "Procesando contenidos en segundo plano..."
                    : "Procesamiento finalizado.";
                threadInfo.textContent = state.threadId
                    ? "Worker Thread #" + state.threadId +
                      " | Solicitud HTTP atendida por el hilo #" + requestThreadId
                    : "";

                if (state.status === "completed") {
                    button.disabled = false;
                    renderResults(state.result, requestThreadId, state.threadId, state.elapsedMilliseconds);
                    return;
                }

                if (state.status === "failed") {
                    button.disabled = false;
                    showError(state.error || "El procesamiento falló.");
                    return;
                }

                timer = window.setTimeout(function () {
                    poll(jobId, requestThreadId);
                }, 250);
            })
            .catch(function (error) {
                showError(error.message);
            });
    }

    button.addEventListener("click", function () {
        button.disabled = true;
        errorBox.classList.add("d-none");
        resultsBox.innerHTML = "";
        statusBox.classList.remove("d-none");
        setProgress(0);
        message.textContent = "Enviando trabajo al Worker Thread...";
        threadInfo.textContent = "";

        var token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        var body = new URLSearchParams();
        body.append("__RequestVerificationToken", token);

        fetch(startUrl, {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: body.toString()
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("No se pudo iniciar el procesamiento.");
                }
                return response.json();
            })
            .then(function (data) {
                window.clearTimeout(timer);
                poll(data.jobId, data.requestThreadId);
            })
            .catch(function (error) {
                showError(error.message);
            });
    });
})();
