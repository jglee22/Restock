mergeInto(LibraryManager.library, {
  RestockSyncPersistentData: function (gameObjectNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    if (typeof FS === "undefined" || typeof FS.syncfs !== "function") {
      SendMessage(gameObjectName, "OnWebGlPersistentSyncFailed", "FS.syncfs is unavailable");
      return;
    }

    try {
      FS.syncfs(false, function (err) {
        if (err) {
          var detail = err.message ? err.message : String(err);
          SendMessage(gameObjectName, "OnWebGlPersistentSyncFailed", detail);
        } else {
          SendMessage(gameObjectName, "OnWebGlPersistentSyncSucceeded");
        }
      });
    } catch (exception) {
      var message = exception && exception.message ? exception.message : String(exception);
      SendMessage(gameObjectName, "OnWebGlPersistentSyncFailed", message);
    }
  }
});
