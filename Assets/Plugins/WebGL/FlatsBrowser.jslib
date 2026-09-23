mergeInto(LibraryManager.library, {
  FlatsGameplayState: function(playing) { window.dispatchEvent(new CustomEvent('flats-gameplay', {detail: {playing: !!playing}})); },
  $FlatsStorageHooks__deps: ['$IDBFS'],
  $FlatsStorageHooks__postset: 'FlatsStorageHooks();',
  $FlatsStorageHooks: function() {
    if (IDBFS.flatsObserved) return;
    IDBFS.flatsObserved = true;
    // Unity autoSyncPersistentDataPath calls IDBFS directly, bypassing FS.syncfs.
    // Install before main (also covers the initial populate from IndexedDB).
    var sync = IDBFS.syncfs.bind(IDBFS);
    IDBFS.syncfs = function(mount, populate, callback) {
      return sync(mount, populate, function(error) {
        window.dispatchEvent(new CustomEvent('flats-storage', {detail: {ok:!error, reason:error ? String(error.name || 'StorageError') : ''}}));
        if(callback) callback(error);
      });
    };
    window.addEventListener('flats-save-retry', function(){FS.syncfs(false, function(){});});
  },
  FlatsStorageMonitor__deps: ['$FlatsStorageHooks'],
  FlatsStorageMonitor: function() { FlatsStorageHooks(); }
});
