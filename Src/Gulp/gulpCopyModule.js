'use strict';
if (require) {
    var gulp = require('gulp');
    var through = require('through2');
    var batch = require('gulp-batch');
    var changed = require('gulp-changed');
    var chmod = require('gulp-chmod');
    var gulpPrint = require('gulp-print');
    var gulpFilter = require('gulp-filter');
    var gulpFlatten = require('gulp-flatten');
}

// module to copy files from source project to destination
var copyModule = (function (gulp) {
    var currentModuleName;
    var folders = {};
    // only the first filespec is watched, so make that the master (non generated) one
    folders['Scripts'] = { folderType: 'script', filespec: ['.js', '.d.ts', '.html'] };
    folders['ViewModels'] = { folderType: 'script', filespec: ['.js', '.d.ts'] };
    folders['Content'] = { folderType: 'script', filespec: ['.less', '.css'] };
    folders['Views/Shared'] = { folderType: 'script', filespec: ['.cshtml'] };
    folders['Authentication'] = { folderType: 'script', filespec: ['.aspx'] };
    folders['ManualLogon'] = { folderType: 'script', filespec: ['.aspx'] };
    folders['bin'] = { folderType: 'bin', filespec: ['{moduleName}.dll', '{moduleName}.pdb'] };

    function getWatchPaths(moduleName) {
        var ret = [];
        for (var folder in folders) {
            if (folders.hasOwnProperty(folder)) {
                ret.push({ folder: getPath(moduleName, folder), filespec: folders[folder].filespec });
            }
        }
        return ret;
    }

    function watchAndCopyChanges(moduleName) {
        var paths = getWatchPaths(moduleName);
        for (var i = 0; i < paths.length; i++) {
            gulp.watch(paths[i].folder + '**/*' + parseFilespec(paths[i].filespec[0], moduleName), batch({ timeout: 1000 }, function (events, cb) {
                events.on('data', function (e) {
                    gulp.start(moduleName);
                }).on('end', cb);
            }));
            console.log('watching folder ' + paths[i].folder + ' for ' + parseFilespec(paths[i].filespec[0], moduleName));
        }
    }

    function parseFilespec(fileSpec, moduleName) {
        return fileSpec.replace('{moduleName}', moduleName);
    }

    // The public method that one calls to copy a module
    function copyModule(moduleName) {
        for (var folder in folders) {
            if (folders.hasOwnProperty(folder)) {
                copyFolder(moduleName, folder);
            }
        }
    }

    function copyFolder(moduleName, folderName) {
        // copy a folder and file spec from the source module to here
        if (!moduleName)
            throw "module name null";
        if (!folderName)
            throw "folderName name null";
        var paths = getPathsForModuleAndFolder(moduleName, folderName);

        // copy to dependency root folder
        gulp.src(paths.scripts, { base: getPath(moduleName, folderName) })
			.pipe(changed(paths.dependencyRootFolder, { hasChanged: changed.compareSha1Digest })) // compareLastModifiedTime
			.pipe(chmod({ owner: { read: true, write: true } }))
			.pipe(gulp.dest(paths.dependencyRootFolder))
            .pipe(gulpPrint({ format: function (filepath) { return "-> " + filepath; }, colors: false }));

        // copy to dependency folder
        gulp.src(paths.scripts, { base: getPath(moduleName, folderName) })
			.pipe(changed(paths.dependencyFolder, { hasChanged: changed.compareSha1Digest }))
			.pipe(chmod({ owner: { read: true, write: true } }))
			.pipe(gulp.dest(paths.dependencyFolder))
            .pipe(gulpPrint({ format: function (filepath) { return "-> " + filepath; }, colors: false }));

        // copy to module folder
        gulp.src(paths.scripts, { base: getPath(moduleName, folderName) })
			.pipe(changed(paths.moduleFolder, { hasChanged: changed.compareSha1Digest }))
			.pipe(chmod({ owner: { read: true, write: true } }))
            .pipe(gulp.dest(paths.moduleFolder))
            .pipe(gulpPrint({ format: function (filepath) { return "-> " + filepath; }, colors: false }));
    }
    
    function getPathsForModuleAndFolder(moduleName, folderName) {
        if (!moduleName)
            throw "module name null";
        if (!folderName)
            throw "folderName name null";
        var prefix = getPath(moduleName, folderName);
        var files = [];
        for (var i = 0; i < folders[folderName].filespec.length; i++) {
            files.push(prefix + '**/*' + folders[folderName].filespec[i].replace("{moduleName}", moduleName));
        }
        var moduleFolder;
		var moduleRoot = "../../../" + currentModuleName;
		moduleFolder = moduleRoot + '/Src/' + currentModuleName + '/' + folderName + '/' + moduleName + '/';
        if (folders[folderName].folderType == 'bin') {
            moduleFolder = moduleRoot + '/Src/' + currentModuleName + '/bin/';
        }
        return {
            scripts: files,
            moduleFolder: moduleFolder,
            dependencyFolder: moduleRoot + '/Dependencies/' + moduleName + '/' + folderName + '/' + moduleName + '/',
            dependencyRootFolder: moduleRoot + '/Dependencies/',
            binFolder: moduleRoot + '/Src/' + currentModuleName + '/bin'
        };
    }

    // gets the folder for a single folder in a module
    function getPath(moduleName, folderName) {
		var moduleRoot = "../../../" + currentModuleName;
        if (!moduleName)
            throw "module name null";
        if (!folderName)
            throw "folderName name null";
        var filespec = folders[folderName];
        if (filespec.folderType == 'script') {
            return '../../../' + moduleName + '/Src/' + moduleName + '/' + folderName + '/' + moduleName + '/';
        }
        if (filespec.folderType == 'bin') {
            return '../../../' + moduleName + '/Src/' + moduleName + '/bin';
        }
        throw "unknown folder type " + filespec.folderType;
    }

    function setCurrentModule(moduleName) {
        currentModuleName = moduleName;
    }

    return {
        copy: copyModule,
        watchAndCopyChanges: watchAndCopyChanges,
        setCurrentModule: setCurrentModule
    };
})(gulp);

if (module) {
    module.exports = copyModule;
}
//# sourceMappingURL=gulpCopyModule.js.map