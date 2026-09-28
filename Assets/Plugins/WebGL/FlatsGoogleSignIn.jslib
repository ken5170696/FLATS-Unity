mergeInto(LibraryManager.library, {
  $FlatsGoogleSignInState: {overlay: null, loading: null, target: null},
  $FlatsGoogleSignInLoad__deps: ['$FlatsGoogleSignInState'],
  $FlatsGoogleSignInLoad: function() {
    if (window.google && window.google.accounts && window.google.accounts.id) return Promise.resolve();
    if (FlatsGoogleSignInState.loading) return FlatsGoogleSignInState.loading;
    FlatsGoogleSignInState.loading = new Promise(function(resolve, reject) {
      var script = document.createElement('script');
      script.src = 'https://accounts.google.com/gsi/client';
      script.async = true; script.defer = true;
      script.onload = function() { resolve(); };
      script.onerror = function() { FlatsGoogleSignInState.loading = null; reject(new Error('Google sign-in script could not be loaded.')); };
      document.head.appendChild(script);
    });
    return FlatsGoogleSignInState.loading;
  },
  $FlatsGoogleSignInClose__deps: ['$FlatsGoogleSignInState'],
  $FlatsGoogleSignInClose: function() {
    var overlay = FlatsGoogleSignInState.overlay;
    FlatsGoogleSignInState.overlay = null;
    if (overlay) overlay.remove();
  },
  FlatsGoogleSignInStart__deps: ['$FlatsGoogleSignInState', '$FlatsGoogleSignInLoad', '$FlatsGoogleSignInClose'],
  FlatsGoogleSignInStart: function(clientIdPtr, noncePtr, targetPtr, titlePtr, cancelPtr) {
    var clientId = UTF8ToString(clientIdPtr), nonce = UTF8ToString(noncePtr), target = UTF8ToString(targetPtr);
    var title = UTF8ToString(titlePtr), cancelLabel = UTF8ToString(cancelPtr);
    FlatsGoogleSignInState.target = target;
    var fail = function(message) { FlatsGoogleSignInClose(); SendMessage(target, 'OnGoogleSignInFailed', String(message)); };
    FlatsGoogleSignInClose();
    // Full-page overlay above the Unity canvas; the Google button is rendered by GIS itself.
    var overlay = document.createElement('div');
    overlay.setAttribute('role', 'dialog'); overlay.setAttribute('aria-modal', 'true');
    overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483000;display:flex;align-items:center;justify-content:center;background:rgba(41,39,43,.72);font-family:sans-serif';
    var panel = document.createElement('div');
    panel.style.cssText = 'background:#f5f5f5;color:#29272b;border:8px solid #cc1966;padding:28px 32px;min-width:280px;max-width:90vw;text-align:center';
    var heading = document.createElement('h2'); heading.textContent = title; heading.style.cssText = 'margin:0 0 18px;font-size:22px';
    var holder = document.createElement('div'); holder.style.cssText = 'display:flex;justify-content:center;min-height:44px';
    var cancel = document.createElement('button'); cancel.type = 'button'; cancel.textContent = cancelLabel;
    cancel.style.cssText = 'margin-top:20px;background:#29272b;color:#fff;border:0;padding:10px 24px;font-size:16px;cursor:pointer';
    cancel.addEventListener('click', function() { FlatsGoogleSignInClose(); SendMessage(target, 'OnGoogleSignInCancelled', ''); });
    panel.appendChild(heading); panel.appendChild(holder); panel.appendChild(cancel); overlay.appendChild(panel);
    document.body.appendChild(overlay);
    FlatsGoogleSignInState.overlay = overlay;
    FlatsGoogleSignInLoad().then(function() {
      if (FlatsGoogleSignInState.overlay !== overlay) return;
      try {
        google.accounts.id.initialize({
          client_id: clientId, nonce: nonce, ux_mode: 'popup', auto_select: false, itp_support: true,
          callback: function(response) {
            if (FlatsGoogleSignInState.overlay !== overlay) return;
            FlatsGoogleSignInClose();
            if (response && response.credential) SendMessage(target, 'OnGoogleIdToken', response.credential);
            else SendMessage(target, 'OnGoogleSignInFailed', 'Google did not return a sign-in token.');
          }
        });
        google.accounts.id.renderButton(holder, {theme: 'outline', size: 'large', text: 'signin_with', shape: 'rectangular', width: 280});
      } catch (error) { fail(error && error.message ? error.message : error); }
    }).catch(function(error) { fail(error && error.message ? error.message : error); });
  },
  FlatsGoogleSignInCancel__deps: ['$FlatsGoogleSignInClose'],
  FlatsGoogleSignInCancel: function() { FlatsGoogleSignInClose(); },
  FlatsGoogleSignOut: function() {
    try { if (window.google && google.accounts && google.accounts.id) google.accounts.id.disableAutoSelect(); } catch (error) {}
  }
});
