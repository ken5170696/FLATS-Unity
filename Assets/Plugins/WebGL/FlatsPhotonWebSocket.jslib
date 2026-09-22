mergeInto(LibraryManager.library, {
  $FlatsPhotonSockets: { next: 1, sockets: {} },
  FlatsPhotonOpen__deps: ['$FlatsPhotonSockets'],
  FlatsPhotonOpen: function (urlPointer) {
    try {
      var url = new URL(UTF8ToString(urlPointer));
      // The NameServer hands out regional master/game hosts. Limit both the
      // domain boundary and documented WSS ports; never accept credentials.
      if (url.protocol !== 'wss:' || url.username || url.password || url.hash ||
          !/(^|\.)(exitgames\.com|photonengine\.io)$/.test(url.hostname) ||
          ['', '443', '19090', '19091', '19093'].indexOf(url.port) < 0) return 0;
      var id = FlatsPhotonSockets.next++;
      var ws = new WebSocket(url.href, 'GpBinaryV16');
      var entry = { ws: ws, state: 0, queue: [], bytes: 0 };
      FlatsPhotonSockets.sockets[id] = entry;
      ws.binaryType = 'arraybuffer';
      ws.onopen = function () { entry.state = 1; };
      ws.onclose = ws.onerror = function () { entry.state = -1; };
      ws.onmessage = function (event) {
        if (!(event.data instanceof ArrayBuffer) || event.data.byteLength < 2 ||
            event.data.byteLength > 1048576 || entry.bytes + event.data.byteLength > 4194304 || entry.queue.length >= 256) {
          entry.state = -1; ws.close(); return;
        }
        entry.queue.push(new Uint8Array(event.data)); entry.bytes += event.data.byteLength;
      };
      return id;
    } catch (_) { return 0; }
  },
  FlatsPhotonState__deps: ['$FlatsPhotonSockets'],
  FlatsPhotonState: function (id) { var e = FlatsPhotonSockets.sockets[id]; return e ? e.state : -1; },
  FlatsPhotonReceive__deps: ['$FlatsPhotonSockets'],
  FlatsPhotonReceive: function (id, pointer, capacity) {
    var e = FlatsPhotonSockets.sockets[id];
    if (!e || !e.queue.length) return 0;
    var data = e.queue[0];
    if (!pointer) return data.length;
    if (capacity < data.length) return -1;
    HEAPU8.set(data, pointer); e.queue.shift(); e.bytes -= data.length;
    return data.length;
  },
  FlatsPhotonSend__deps: ['$FlatsPhotonSockets'],
  FlatsPhotonSend: function (id, pointer, length) {
    var e = FlatsPhotonSockets.sockets[id];
    if (!e || e.state !== 1 || length < 1 || length > 1048576 || e.ws.bufferedAmount + length > 4194304) return 0;
    try { e.ws.send(HEAPU8.slice(pointer, pointer + length)); return 1; } catch (_) { return 0; }
  },
  FlatsPhotonClose__deps: ['$FlatsPhotonSockets'],
  FlatsPhotonClose: function (id) {
    var e = FlatsPhotonSockets.sockets[id]; if (!e) return;
    e.ws.onopen = e.ws.onmessage = e.ws.onclose = e.ws.onerror = null;
    e.ws.close(); delete FlatsPhotonSockets.sockets[id];
  },
  FlatsPhotonAppId: function (pointer, capacity) {
    var meta = document.querySelector('meta[name="flats-photon-app-id"]');
    var value = meta ? meta.content.trim() : '';
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)) return 0;
    stringToUTF8(value, pointer, capacity); return 1;
  }
});
