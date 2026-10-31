// Landing page: fills in the latest release's version and Linux download from GitHub. If the request fails (offline,
// rate limit) the links keep pointing at the Releases page, so nothing breaks.
(function () {
  var version = document.querySelector('[data-cx-version]');
  var linux = document.querySelector('[data-cx-linux]');
  if (!version || !window.fetch) return;
  fetch('https://api.github.com/repos/somedudenamedtony/costas/releases/latest')
    .then(function (r) { return r.ok ? r.json() : null; })
    .then(function (release) {
      if (!release) return;
      version.textContent = release.name || release.tag_name;
      var asset = (release.assets || []).find(function (a) { return /-linux-x64\.tar\.gz$/.test(a.name); });
      if (asset && linux) linux.href = asset.browser_download_url;
    })
    .catch(function () {});
})();
