// JellyShare - adds a "Share publicly" entry to the item context menu of the
// Jellyfin web interface. Injected into index.html by the server.
(function () {
    'use strict';

    var LABEL = 'Share publicly';
    var PLUGIN_ID = 'eb5d7894-8eef-4b36-aa6f-5d124e828ce1';

    // Read the item id from the detail page URL (#!/details?id=XXXXXXXX...).
    function getDetailItemId() {
        var hash = window.location.hash || '';
        var m = hash.match(/[?&]id=([a-f0-9]{32})/i);
        return m ? m[1] : null;
    }

    // Fetch the default validity from the plugin settings (fallback 7 days).
    function getDefaultDays() {
        try {
            return ApiClient.getPluginConfiguration(PLUGIN_ID).then(function (c) {
                return (c && typeof c.DefaultExpiryDays === 'number') ? c.DefaultExpiryDays : 7;
            }, function () { return 7; });
        } catch (e) {
            return Promise.resolve(7);
        }
    }

    // Ask the user for the validity. Resolves to a number of days, or null on cancel.
    function promptExpiry() {
        return getDefaultDays().then(function (def) {
            return askDaysDialog(def);
        });
    }

    // Styled dialog for entering the validity. Promise -> days (number) or null.
    function askDaysDialog(def) {
        return new Promise(function (resolve) {
            var overlay = document.createElement('div');
            overlay.style.cssText = 'position:fixed;inset:0;z-index:100000;background:rgba(0,0,0,.6);' +
                'display:flex;align-items:center;justify-content:center;padding:16px;';

            var card = document.createElement('div');
            card.style.cssText = 'background:#202020;color:#fff;max-width:440px;width:100%;border-radius:8px;' +
                'padding:24px;box-shadow:0 8px 40px rgba(0,0,0,.6);box-sizing:border-box;';

            var btn = 'border:0;border-radius:4px;padding:9px 18px;font-size:14px;cursor:pointer;';
            card.innerHTML =
                '<h2 style="margin:0 0 12px;text-align:center;font-weight:400;">Share publicly</h2>' +
                '<label class="js-lbl" style="display:block;margin:0 0 8px;">' +
                    'Validity in days (0 = never expires):</label>' +
                '<input type="number" min="0" class="js-days" style="width:100%;box-sizing:border-box;' +
                    'background:#101010;color:#fff;border:1px solid #444;border-radius:4px;padding:9px;' +
                    'font-size:16px;">' +
                '<div style="display:flex;gap:8px;justify-content:flex-end;margin-top:18px;flex-wrap:wrap;">' +
                    '<button class="js-cancel" style="' + btn + 'background:#333;color:#fff;">Cancel</button>' +
                    '<button class="js-ok" style="' + btn + 'background:#00a4dc;color:#fff;">Create</button>' +
                '</div>';

            var input = card.querySelector('.js-days');
            input.value = String(def);

            var done = false;
            function finish(value) {
                if (done) { return; }
                done = true;
                document.removeEventListener('keydown', onKey, true);
                overlay.remove();
                resolve(value);
            }
            function submit() {
                var n = parseInt(input.value, 10);
                finish(isNaN(n) || n < 0 ? def : n);
            }
            function onKey(e) {
                if (e.key === 'Enter') { e.preventDefault(); submit(); }
                else if (e.key === 'Escape') { e.preventDefault(); finish(null); }
            }

            overlay.addEventListener('click', function (e) { if (e.target === overlay) { finish(null); } });
            card.querySelector('.js-cancel').addEventListener('click', function () { finish(null); });
            card.querySelector('.js-ok').addEventListener('click', submit);
            document.addEventListener('keydown', onKey, true);

            overlay.appendChild(card);
            document.body.appendChild(overlay);
            input.focus();
            input.select();
        });
    }

    function createShare(itemId, days) {
        return ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl('JellyShare/Shares/' + itemId + '?expiryDays=' + encodeURIComponent(days)),
            dataType: 'json'
        });
    }

    // Show the resulting link in a dialog and copy it to the clipboard.
    function notify(url) {
        var copied = false;
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(url).then(function () { copied = true; }).catch(function () {});
        }

        var overlay = document.createElement('div');
        overlay.style.cssText = 'position:fixed;inset:0;z-index:100000;background:rgba(0,0,0,.6);' +
            'display:flex;align-items:center;justify-content:center;padding:16px;';

        var card = document.createElement('div');
        card.style.cssText = 'background:#202020;color:#fff;max-width:520px;width:100%;border-radius:8px;' +
            'padding:24px;box-shadow:0 8px 40px rgba(0,0,0,.6);box-sizing:border-box;';

        var btn = 'border:0;border-radius:4px;padding:9px 18px;font-size:14px;cursor:pointer;';
        card.innerHTML =
            '<h2 style="margin:0 0 12px;text-align:center;font-weight:400;">JellyShare</h2>' +
            '<p style="margin:0 0 10px;">Public link' +
                '<span class="js-hint" style="color:#4caf50;"></span>:</p>' +
            '<textarea readonly rows="3" class="js-url" style="width:100%;box-sizing:border-box;' +
                'resize:none;background:#101010;color:#fff;border:1px solid #444;border-radius:4px;' +
                'padding:8px;font-size:14px;word-break:break-all;"></textarea>' +
            '<div style="display:flex;gap:8px;justify-content:flex-end;margin-top:18px;flex-wrap:wrap;">' +
                '<button class="js-copy" style="' + btn + 'background:#333;color:#fff;">Copy</button>' +
                '<button class="js-open" style="' + btn + 'background:#333;color:#fff;">Open</button>' +
                '<button class="js-close" style="' + btn + 'background:#00a4dc;color:#fff;">Close</button>' +
            '</div>';

        var ta = card.querySelector('.js-url');
        ta.value = url;
        var hint = card.querySelector('.js-hint');
        if (copied) { hint.textContent = ' (copied to clipboard)'; }

        function close() { overlay.remove(); }
        overlay.addEventListener('click', function (e) { if (e.target === overlay) { close(); } });
        card.querySelector('.js-close').addEventListener('click', close);
        card.querySelector('.js-open').addEventListener('click', function () { window.open(url, '_blank'); });
        card.querySelector('.js-copy').addEventListener('click', function () {
            ta.select();
            if (navigator.clipboard && window.isSecureContext) {
                navigator.clipboard.writeText(url).catch(function () {});
            } else {
                document.execCommand('copy');
            }
            hint.textContent = ' (copied to clipboard)';
        });

        overlay.appendChild(card);
        document.body.appendChild(overlay);
        ta.focus();
        ta.select();
    }

    // Add our entry to an opened action sheet.
    function enhance(sheet) {
        if (sheet.__jellyshare) { return; }
        var items = sheet.querySelectorAll('.actionSheetMenuItem');
        if (!items.length) { return; }

        var itemId = getDetailItemId();
        if (!itemId) { return; }            // detail pages only
        sheet.__jellyshare = true;

        // Clone the last real entry -> automatically inherits the markup/styling
        // of the running Jellyfin version.
        var template = items[items.length - 1];
        var clone = template.cloneNode(true);

        // Icon: drop the inherited icon (e.g. "share") and add the globe ("public").
        // The glyph comes from the class (.material-icons.public:before), not from the
        // text - so swap the class instead of setting text, otherwise two icons render.
        var icon = clone.querySelector('.material-icons');
        if (icon) {
            var keep = ['actionsheetMenuItemIcon', 'listItemIcon', 'listItemIcon-transparent', 'material-icons'];
            Array.prototype.slice.call(icon.classList).forEach(function (c) {
                if (keep.indexOf(c) === -1) { icon.classList.remove(c); }
            });
            icon.classList.add('public');
            icon.textContent = '';
        }

        var text = clone.querySelector('.actionSheetItemText') ||
                   clone.querySelector('.listItemBodyText') || clone;
        text.textContent = LABEL;

        clone.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            var close = sheet.querySelector('.btnCloseActionSheet');
            if (close) { close.click(); }

            promptExpiry().then(function (days) {
                if (days === null) { return; }   // cancelled
                createShare(itemId, days).then(function (s) {
                    notify(ApiClient.serverAddress() + s.Url);
                }).catch(function () {
                    alert('JellyShare: could not create link.');
                });
            });
        }, true);

        template.parentNode.appendChild(clone);
    }

    // Action sheets are inserted dynamically -> catch them with a MutationObserver.
    var observer = new MutationObserver(function (mutations) {
        for (var i = 0; i < mutations.length; i++) {
            var nodes = mutations[i].addedNodes;
            for (var j = 0; j < nodes.length; j++) {
                var n = nodes[j];
                if (n.nodeType !== 1) { continue; }
                if (n.classList && n.classList.contains('actionSheet')) {
                    enhance(n);
                } else if (n.querySelector) {
                    var s = n.querySelector('.actionSheet');
                    if (s) { enhance(s); }
                }
            }
        }
    });

    function start() {
        if (!window.ApiClient) { return setTimeout(start, 500); }
        observer.observe(document.body, { childList: true, subtree: true });
    }

    start();
})();
