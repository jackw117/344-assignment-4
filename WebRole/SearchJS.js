var word;
console.log("correct file");

$('#searchBox').keyup(function () {
    console.log("key press");
    word = $(this).val().toLowerCase().trim();
    if (word.length != 0) {
        callWebMethod();
    } else {
        $('#results').empty();
    }
});

function callWebMethod() {
    $.ajax({
        type: "POST",
        url: "QuerySuggest.asmx/searchTrie",
        data: JSON.stringify({
            "search": word
        }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (msg) {
            console.log("success");
            $('#results').empty();
            var data = eval(msg);
            for (var key in data.d) {
                var result = data.d[key];
                $("#results").append("<p class='suggestion' id='" + result + "'>" + result + "</p>");
            }
        },
        error: function (msg) {
            console.log("error");
            console.log(msg);
        }
    });
}

$("#search").click(function () {
    console.log(word);
    $.ajax({
        crossDomain: true,
        contentType: "application/json; charset=utf-8",
        url: "http://ec2-54-191-100-37.us-west-2.compute.amazonaws.com/names.php",
        data: { player: word },
        dataType: "jsonp",
        success: onDataReceived,
        error: function (msg) {
            console.log(msg);
        }
    });

    $.ajax({
        type: "POST",
        url: "Admin.asmx/getPageTitle",
        data: JSON.stringify({
            "title": word
        }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (msg) {
            console.log(msg);
            var data = eval(msg);
            $("#pages").empty();
            for (var key in data.d) {
                $("#pages").append("<p>" + data.d[key] + "</p>");
            }
            console.log("success");
        },
        error: function (msg) {
            console.log("error");
            console.log(msg);
        }
    });
});

function onDataReceived(data) {
    console.log(data[0]);
    $("#player").empty();
    if (data[0] != null) {
        $("#player").append(
            "<table><tr><th>Name</th><th>Team</th><th>GP</th><th>Min</th><th>FGM</th><th>FGA</th><th>FG%</th><th>3PM</th><th>3PA</th>" + 
            "<th>3P%</th><th>FTM</th><th>FTA</th><th>FT%</th><th>OREB</th><th>DREB</th><th>REB</th><th>AST</th><th>STL</th><th>BLK</th>" +
            "<th>TOV</th><th>PF</th><th>PPG</th></tr>" +
            "<tr><td>" + data[0].Name + "</td><td>" + data[0].General_Team + "</td><td>" + data[0].General_GP + "</td><td>" + data[0].General_Min +
            "</td><td>" + data[0].FG_M + "</td><td>" + data[0].FG_A + "</td><td>" + data[0].FG_Pct + "</td><td>" + data[0]["3PT_M"] +
            "</td><td>" + data[0]["3PT_A"] + "</td><td>" + data[0]["3PT_Pct"] + "</td><td>" + data[0].FT_M + "</td><td>" +
            data[0].FT_A + "</td><td>" + data[0].FT_Pct + "</td><td>" + data[0].Rebounds_Off + "</td><td>" + data[0].Rebounds_Def +
            "</td><td>" + data[0].Rebounds_Tot + "</td><td>" + data[0].Misc_Ast + "</td><td>" + data[0].Misc_Stl + "</td><td>" +
            data[0].Misc_Blk + "</td><td>" + data[0].Misc_TO + "</td><td>" + data[0].Misc_PF + "</td><td>" + data[0].Misc_PPG + "</td></table>");
    }
}

$("#results").on("click", ".suggestion", function () {
    word = $(this).attr("id");
    $("#searchBox").val(word);
    callWebMethod();
});

setInterval(function () {
    console.log("5 minutes");
    $.ajax({
        type: "POST",
        url: "QuerySuggest.asmx/searchTrie",
        data: JSON.stringify({
            "search": "a"
        }),
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        success: function (msg) {
            console.log(msg);
            console.log("success");
        },
        error: function (msg) {
            console.log("error");
            console.log(msg);
        }
    });
}, 300000);