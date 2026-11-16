// Turns raw status codes such as "InProgress" or "NoShowPatient" into "In progress" and "No show patient".
(function () {
  var CODE = /^[A-Z][a-z]+(?:[A-Z][a-z]+)+$/;
  function humanize(s) {
    var words = s.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
    return words.charAt(0).toUpperCase() + words.slice(1);
  }
  document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.th-pill').forEach(function (pill) {
      pill.childNodes.forEach(function (node) {
        if (node.nodeType !== 3) return;
        var text = node.textContent.trim();
        if (CODE.test(text)) node.textContent = node.textContent.replace(text, humanize(text));
      });
    });
  });
})();
