namespace TikTokUploadMethod;

internal static class BypassScript
{
    public const string Code = @"
(function() {
  'use strict';
  if (window.__qfpb_installed) return;
  window.__qfpb_installed = true;

  window.__qfpb_active = true;
  window.__qfpb_promoActive = false;
  window.__qfpb_force = true;

  var CAPTION_SUFFIX = ' Upload Method: ';
  var MENTION_QUERY = 'oncxzy';
  var FULL_MARKER = 'Upload Method: @oncxzy';

  function isUploadPage() {
    var u = window.location.href;
    return u.indexOf('/upload') !== -1
        || u.indexOf('/creator-center') !== -1
        || u.indexOf('/tiktokstudio') !== -1
        || u.indexOf('/tiktok-studio') !== -1
        || u.indexOf('/creator/upload') !== -1;
  }

  function getEditor() {
    return document.querySelector('.public-DraftEditor-content');
  }

  function alreadyHasMention(el) {
    try { return (el.innerText || '').indexOf(FULL_MARKER) !== -1; } catch(e) { return false; }
  }

  function moveToEnd(el) {
    try {
      el.focus();
      var sel = window.getSelection();
      var range = document.createRange();
      range.selectNodeContents(el);
      range.collapse(false);
      sel.removeAllRanges();
      sel.addRange(range);
    } catch(e) {}
  }

  function typeChar(el, ch) {
    try {
      el.dispatchEvent(new KeyboardEvent('keydown', { key: ch, bubbles: true }));
      document.execCommand('insertText', false, ch);
      el.dispatchEvent(new KeyboardEvent('keyup', { key: ch, bubbles: true }));
    } catch(e) {}
  }

  function getFirstMentionItem() {
    var selectors = [
      '[class*=""mention-suggestion-item""]',
      '.mention-list-popover [role=""option""]',
      '[class*=""mention-list-popover""] [role=""option""]',
      '[id^=""mention-option""]'
    ];
    for (var i = 0; i < selectors.length; i++) {
      try {
        var found = document.querySelector(selectors[i]);
        if (found) return found;
      } catch(e) {}
    }
    return null;
  }

  function dropdownIsVisible() {
    return !!getFirstMentionItem();
  }

  function selectMentionItem(el, editor) {
    try {
      var item = getFirstMentionItem();
      if (item) {
        ['mouseenter','mouseover','mousedown','mouseup','click'].forEach(function(evt) {
          try {
            item.dispatchEvent(new MouseEvent(evt, { bubbles: true, cancelable: true }));
          } catch(e) {}
        });
      }
      setTimeout(function() {
        try {
          editor.focus();
          editor.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true }));
          editor.dispatchEvent(new KeyboardEvent('keyup',   { key: 'Enter', keyCode: 13, which: 13, bubbles: true }));
        } catch(e) {}
      }, 60);
      return true;
    } catch(e) { return false; }
  }

  function cleanupPartialMention(editor) {
    try {
      var text = editor.innerText || '';
      var atIdx = text.lastIndexOf('@');
      if (atIdx !== -1 && atIdx > text.lastIndexOf('Upload Method')) {
        var cleanText = text.substring(0, atIdx) + '@oncxzy';
        moveToEnd(editor);
        document.execCommand('selectAll', false, null);
        document.execCommand('insertText', false, cleanText);
      }
    } catch(e) {}
  }

  function injectMentionThenPost(postFn) {
    var el = getEditor();
    if (!el) { postFn(); return; }
    if (alreadyHasMention(el)) { postFn(); return; }

    var step = 0;
    var dropdownRetries = 0;
    var MAX_DROPDOWN_RETRIES = 12;

    var iv = setInterval(function() {
      var editor = getEditor();
      if (!editor) { clearInterval(iv); postFn(); return; }

      if (step === 0) {
        moveToEnd(editor);
        document.execCommand('insertText', false, CAPTION_SUFFIX);
        step = 1;

      } else if (step === 1) {
        moveToEnd(editor);
        typeChar(editor, '@');
        step = 2;

      } else if (step === 2) {
        moveToEnd(editor);
        document.execCommand('insertText', false, MENTION_QUERY);
        step = 3;
        dropdownRetries = 0;

      } else if (step === 3) {
        if (dropdownIsVisible()) {
          selectMentionItem(editor, editor);
          step = 4;
        } else {
          dropdownRetries++;
          if (dropdownRetries >= MAX_DROPDOWN_RETRIES) {
            clearInterval(iv);
            cleanupPartialMention(editor);
            setTimeout(postFn, 150);
          }
        }

      } else if (step === 4) {
        clearInterval(iv);
        setTimeout(postFn, 400);
      }
    }, 150);
  }

  function findPostButton() {
    var selectors = [
      '[data-e2e=""post_video_button""]',
      '[data-e2e=""btn-post""]',
      'button[class*=""post""][class*=""btn""]',
      'button[class*=""Post""][class*=""Btn""]',
      '[class*=""post-button""]',
      '[class*=""PostButton""]',
      '[class*=""submitBtn""]',
      '[class*=""submit-btn""]'
    ];
    for (var i = 0; i < selectors.length; i++) {
      try {
        var found = document.querySelector(selectors[i]);
        if (found && found.tagName) return found;
      } catch(e) {}
    }
    return null;
  }

  var __postBtnHooked = false;

  function hookPostButton() {
    if (__postBtnHooked) return;
    var btn = findPostButton();
    if (!btn) return;
    __postBtnHooked = true;

    btn.addEventListener('click', function(e) {
      if (!isUploadPage()) return;
      var el = getEditor();
      if (!el || alreadyHasMention(el)) return;

      e.stopImmediatePropagation();
      e.preventDefault();

      injectMentionThenPost(function() {
        __postBtnHooked = false;
        btn.click();
      });
    }, true);
  }

  var observer = new MutationObserver(function() {
    hookPostButton();
  });

  if (document.body) {
    observer.observe(document.body, { childList: true, subtree: true });
  } else {
    document.addEventListener('DOMContentLoaded', function() {
      observer.observe(document.body, { childList: true, subtree: true });
    });
  }

  setInterval(function() {
    if (!isUploadPage()) { __postBtnHooked = false; return; }
    hookPostButton();
  }, 1000);

  window.addEventListener('popstate', function() {
    __postBtnHooked = false;
  });

  function deepClean(obj, depth) {
    if (!obj || typeof obj !== 'object' || depth > 8) return;
    var forbidden = ['draft', 'canvas_config', 'vedit_segment_info'];
    for (var i = 0; i < forbidden.length; i++) {
      try {
        if (Object.prototype.hasOwnProperty.call(obj, forbidden[i])) {
          delete obj[forbidden[i]];
        }
      } catch (e) {}
    }
    try { if (obj.cloud_edit_is_use_video_canvas !== undefined) obj.cloud_edit_is_use_video_canvas = false; } catch (e) {}
    try { if (obj.post_type === 2) obj.post_type = 3; } catch (e) {}
    try { if (obj.enter_post_page_from !== undefined) obj.enter_post_page_from = 1; } catch (e) {}
    try {
      if (Array.isArray(obj)) {
        for (var j = 0; j < obj.length; j++) deepClean(obj[j], depth + 1);
      } else {
        var keys = Object.keys(obj);
        for (var k = 0; k < keys.length; k++) {
          var v = obj[keys[k]];
          if (v && typeof v === 'object') deepClean(v, depth + 1);
        }
      }
    } catch (e) {}
  }

  function looksLikeUploadPayload(value) {
    if (!value || typeof value !== 'object') return false;
    if (value.single_post_req_list) return true;
    if (value.vedit_common_info) return true;
    if (value.post_common_info) return true;
    if (value.aweme_v1_post && (value.creation_id || value.video_id || value.vid)) return true;
    if (value.creation_id && (value.text !== undefined || value.markup_text !== undefined)) return true;
    return false;
  }

  var originalStringify = JSON.stringify;
  var ourStringify = null;

  function makeHook(orig) {
    return function(value, replacer, space) {
      var shouldRun = window.__qfpb_force === true || isUploadPage();
      if (shouldRun && value && typeof value === 'object') {
        try {
          if (looksLikeUploadPayload(value) && window.__qfpb_active) {
            try { deepClean(value, 0); } catch (e) {}
          }
        } catch (e) {}
      }
      return orig.apply(this, arguments);
    };
  }

  function installHook() {
    try {
      ourStringify = makeHook(originalStringify);
      JSON.stringify = ourStringify;
    } catch (e) {}
  }

  installHook();

  setInterval(function() {
    try {
      if (JSON.stringify !== ourStringify) {
        var current = JSON.stringify;
        if (current !== originalStringify) originalStringify = current;
        ourStringify = makeHook(originalStringify);
        JSON.stringify = ourStringify;
      }
    } catch (e) {}
  }, 1500);
})();
";
}
