mergeInto(LibraryManager.library, {
  $FlatsSaveDialogs: {},
  $FlatsSaveDialog__deps: ['$FlatsSaveDialogs'],
  $FlatsSaveDialog: function(target, token, title) {
    var dialog = document.createElement('dialog');
    var heading = document.createElement('h2'); heading.textContent = title; dialog.appendChild(heading);
    var state = {dialog: dialog, settled: false, reader: null, url: null};
    state.finish = function(status, extra) {
      if (state.settled) return;
      state.settled = true;
      var reply = Object.assign({token: token, status: status}, extra || {});
      state.dispose();
      SendMessage(target, 'OnBrowserSaveReply', JSON.stringify(reply));
    };
    state.dispose = function() {
      state.settled = true;
      if (state.reader && state.reader.readyState === 1) state.reader.abort();
      if (state.url) { var url = state.url; setTimeout(function(){URL.revokeObjectURL(url);}, 60000); }
      dialog.remove();
      if (FlatsSaveDialogs[target] === state) delete FlatsSaveDialogs[target];
    };
    var close = document.createElement('button'); close.textContent = 'Cancel'; close.type = 'button';
    close.addEventListener('click', function(){state.finish('cancel');}); dialog.appendChild(close);
    dialog.addEventListener('cancel', function(event){event.preventDefault();state.finish('cancel');});
    FlatsSaveDialogs[target] = state;
    document.body.appendChild(dialog);
    try {dialog.showModal();} catch(error) {state.dispose();throw error;}
    return state;
  },
  FlatsSaveUpload__deps: ['$FlatsSaveDialog'],
  FlatsSaveUpload: function(targetPtr, tokenPtr) {
    var target = UTF8ToString(targetPtr), token = UTF8ToString(tokenPtr), state;
    try {
      state = FlatsSaveDialog(target, token, 'Import FLATS save');
      var help = document.createElement('p'); help.textContent = 'Choose a FLATS JSON export or legacy .dat save (maximum 1 MiB). You will review it before replacing your current save.';
      state.dialog.insertBefore(help, state.dialog.lastChild);
      var input = document.createElement('input'); input.type = 'file'; input.accept = '.json,.dat'; input.setAttribute('aria-label', 'FLATS save file');
      input.addEventListener('change', function(){
        var file = input.files[0]; if (!file) return;
        if (!file.size || file.size > 1048576) {state.finish('error', {error:'Save file is empty or exceeds 1 MiB.'});return;}
        input.disabled = true;
        var reader = state.reader = new FileReader();
        reader.onerror = function(){state.finish('error', {error:'The browser could not read this file.'});};
        reader.onload = function(){
          if (state.settled) return;
          try {
            var bytes = new Uint8Array(reader.result), binary = '';
            for (var i = 0; i < bytes.length; i += 8192) binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 8192));
            state.finish('file', {name:file.name, data:btoa(binary)});
          } catch(error) {state.finish('error', {error:String(error.message || error)});}
        };
        reader.readAsArrayBuffer(file);
      });
      state.dialog.insertBefore(input, state.dialog.lastChild);
    } catch(error) {
      if (state) state.finish('error', {error:String(error.message || error)});
      else SendMessage(target, 'OnBrowserSaveReply', JSON.stringify({token:token,status:'error',error:String(error.message || error)}));
    }
  },
  FlatsSaveDownload__deps: ['$FlatsSaveDialog'],
  FlatsSaveDownload: function(targetPtr, tokenPtr, namePtr, jsonPtr) {
    var target = UTF8ToString(targetPtr), token = UTF8ToString(tokenPtr), state;
    try {
      var json = UTF8ToString(jsonPtr), name = UTF8ToString(namePtr);
      var blob = new Blob([json], {type:'application/json;charset=utf-8'});
      if (blob.size > 1048576) throw new Error('Save export exceeds 1 MiB.');
      state = FlatsSaveDialog(target, token, 'Download FLATS save');
      var link = document.createElement('a'); state.url = URL.createObjectURL(blob);
      link.href = state.url; link.download = name; link.textContent = 'Download save JSON';
      link.addEventListener('click', function(){setTimeout(function(){state.finish('download');}, 0);});
      state.dialog.insertBefore(link, state.dialog.lastChild);
    } catch(error) {
      if (state) state.finish('error', {error:String(error.message || error)});
      else SendMessage(target, 'OnBrowserSaveReply', JSON.stringify({token:token,status:'error',error:String(error.message || error)}));
    }
  },
  FlatsShareDownload__deps: ['$FlatsSaveDialog'],
  FlatsShareDownload: function(targetPtr, tokenPtr, namePtr, pngPtr, textPtr) {
    var target = UTF8ToString(targetPtr), token = UTF8ToString(tokenPtr), state;
    try {
      var encoded = UTF8ToString(pngPtr);
      if (!encoded.length || encoded.length > 22369624) throw new Error('Share image exceeds 16 MiB.');
      var binary = atob(encoded), bytes = new Uint8Array(binary.length);
      for(var i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
      var signature = [137,80,78,71,13,10,26,10];
      if(bytes.length > 16777216 || !signature.every(function(value,index){return bytes[index] === value;})) throw new Error('Share image is not a valid PNG.');
      state = FlatsSaveDialog(target, token, 'Share FLATS');
      var text = document.createElement('textarea'); text.readOnly = true;
      text.value = UTF8ToString(textPtr); text.setAttribute('aria-label', 'Share text'); text.rows = 3; text.cols = 44;
      state.dialog.insertBefore(text, state.dialog.lastChild);
      var notice = document.createElement('p'); notice.textContent = 'Copy the website link, then download the PNG to attach it to your message. Check browser downloads to confirm the saved file.';
      state.dialog.insertBefore(notice, state.dialog.lastChild);
      var copy = document.createElement('button'); copy.type = 'button'; copy.textContent = 'Copy share text';
      copy.addEventListener('click', function(){
        text.focus();text.select();
        if(!navigator.clipboard || !navigator.clipboard.writeText){notice.textContent = 'Clipboard unavailable. Copy the selected text manually.';return;}
        navigator.clipboard.writeText(text.value).then(function(){notice.textContent = 'Share text copied.';},function(){notice.textContent = 'Clipboard denied. Copy the selected text manually.';});
      });
      state.dialog.insertBefore(copy, state.dialog.lastChild);
      var link = document.createElement('a'); state.url = URL.createObjectURL(new Blob([bytes], {type:'image/png'}));
      link.href = state.url; link.download = UTF8ToString(namePtr); link.textContent = 'Download share PNG';
      link.addEventListener('click', function(){setTimeout(function(){state.finish('download');},0);});
      state.dialog.insertBefore(link, state.dialog.lastChild);
    } catch(error) {
      if(state) state.finish('error',{error:String(error.message || error)});
      else SendMessage(target,'OnBrowserSaveReply',JSON.stringify({token:token,status:'error',error:String(error.message || error)}));
    }
  },
  FlatsSaveFlush__deps: ['$FlatsSaveDialogs'],
  FlatsSaveFlush: function(targetPtr, tokenPtr) {
    var target = UTF8ToString(targetPtr), token = UTF8ToString(tokenPtr);
    var job = {settled:false};
    job.dispose = function(){job.settled = true;clearTimeout(job.timer);if(FlatsSaveDialogs[target] === job)delete FlatsSaveDialogs[target];};
    function finish(error) {
      if(job.settled)return;
      job.dispose();
      SendMessage(target, 'OnBrowserSaveReply', JSON.stringify({token:token,status:error?'error':'saved',error:error?'Browser storage could not be confirmed. Allow site storage or free space, then retry.':''}));
    }
    FlatsSaveDialogs[target] = job;
    job.timer = setTimeout(function(){finish(new Error('Storage timeout'));}, 15000);
    try {FS.syncfs(false, finish);} catch(error) {finish(error);}
  },
  FlatsSaveCancel__deps: ['$FlatsSaveDialogs'],
  FlatsSaveCancel: function(targetPtr) {
    var state = FlatsSaveDialogs[UTF8ToString(targetPtr)];
    if (state) state.dispose();
  }
});
