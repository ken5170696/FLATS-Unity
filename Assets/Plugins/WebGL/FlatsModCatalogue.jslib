mergeInto(LibraryManager.library, {
  $FlatsModRequests: {next: 1, requests: {}},
  FlatsModFetch__deps: ['$FlatsModRequests'],
  FlatsModFetch: function(urlPtr) {
    var id = FlatsModRequests.next++;
    var job = {controller: new AbortController(), status: 0};
    FlatsModRequests.requests[id] = job;
    job.timer = setTimeout(function() { job.controller.abort(); }, 90000);
    fetch(UTF8ToString(urlPtr), {signal: job.controller.signal, redirect: 'error', credentials: 'omit', cache: 'no-store'})
      .then(async function(response) {
        if (!response.ok) throw new Error('HTTP ' + response.status);
        var limit = 2 * 1024 * 1024;
        if (Number(response.headers.get('Content-Length')) > limit) throw new Error('Response too large');
        var reader = response.body.getReader(), parts = [], size = 0;
        while (true) {
          var part = await reader.read();
          if (part.done) break;
          size += part.value.length;
          if (size > limit) { await reader.cancel(); throw new Error('Response too large'); }
          parts.push(part.value);
        }
        job.data = new Uint8Array(size);
        var offset = 0;
        parts.forEach(function(part) { job.data.set(part, offset); offset += part.length; });
        job.status = size + 1;
      }).catch(function() { job.status = -1; }).finally(function() { clearTimeout(job.timer); });
    return id;
  },
  FlatsModPoll__deps: ['$FlatsModRequests'],
  FlatsModPoll: function(id) { return FlatsModRequests.requests[id].status; },
  FlatsModRead__deps: ['$FlatsModRequests'],
  FlatsModRead: function(id, ptr) { HEAPU8.set(FlatsModRequests.requests[id].data, ptr); },
  FlatsModRelease__deps: ['$FlatsModRequests'],
  FlatsModRelease: function(id) {
    var job = FlatsModRequests.requests[id];
    clearTimeout(job.timer); job.controller.abort(); delete FlatsModRequests.requests[id];
  }
});
