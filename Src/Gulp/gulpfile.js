if (require) {
    var gulp = require('gulp-param')(require('gulp'), process.argv);
    var del = require('del');
    var path = require('path');
    var chmod = require('gulp-chmod');
    var changed = require('gulp-changed');
    var copyModule = require('./gulpCopyModule');
}
dependentModules = [];
currentModule = "";

gulp.task('setModule', function (moduleName) {
    dependentModules = [];
    switch (moduleName.toLowerCase()) {
		case "web.case": 
		case "web.test": 
        case "web.administration":
			dependentModules.push("Web.Foundation", "Web.Presentation", "Web.SMB");
			break;
        case "web.time":
        case "web.workflow":
        case "web.expenses":
        case "web.matterworks":
		case "web.smb": 
			dependentModules.push("Web.Foundation", "Web.Presentation");
			break;
        case "web.presentation":
            dependentModules.push("Web.Foundation");
            break;

        default:
	}
    copyModule.setCurrentModule(moduleName);
    currentModule = moduleName;
});

gulp.task('WatchWeb.Foundation', ['setModule', 'Web.Foundation'], function () {
    copyModule.watchAndCopyChanges('Web.Foundation');
});

gulp.task('Web.Foundation', function (moduleName, modulesPath, watch) {
    copyModule.setCurrentModule(moduleName);
    console.log("Copying dependencies from Web.Foundation -> " + moduleName);
    copyModule.copy('Web.Foundation');
    //if (watch) {
    //    copyModule.watchAndCopyChanges('Web.Foundation');
    //}
});

gulp.task('WatchWeb.Presentation', ['setModule', 'Web.Presentation'], function () {
    copyModule.watchAndCopyChanges('Web.Presentation');
});

gulp.task('Web.Presentation', function (moduleName, modulesPath, watch) {
    copyModule.setCurrentModule(moduleName);
	copyModule.setModulesPath(modulesPath);
    console.log("Copying dependencies from Web.Presentation -> " + moduleName);
    copyModule.copy('Web.Presentation');
    //if (watch) {
    //    copyModule.watchAndCopyChanges('Web.Presentation');
    //}
});

gulp.task('WatchWeb.SMB', ['setModule', 'Web.SMB'], function () {
    copyModule.watchAndCopyChanges('Web.SMB');
});

gulp.task('Web.SMB', function (moduleName, modulesPath, watch) {
    copyModule.setCurrentModule(moduleName);
    console.log("Copying dependencies from Web.SMB -> " + moduleName);
    copyModule.copy('Web.SMB');
    //if (watch) {
    //    copyModule.watchAndCopyChanges('Web.SMB');
    //}
});

gulp.task('WatchAll', ['setModule', 'All'], function () {
    for (var i = 0; i < dependentModules.length; i++) {
        copyModule.watchAndCopyChanges(dependentModules[i]);
    }
});

gulp.task('All', ['setModule'], function (watch) {
    for (var i = 0; i < dependentModules.length; i++) {
	    console.log("Copying dependencies from " + dependentModules[i] + " -> " + currentModule);
	    copyModule.copy(dependentModules[i]);
	}
    //if (watch) {
    //    for (var i = 0; i < dependentModules.length; i++) {
    //        copyModule.watchAndCopyChanges(dependentModules[i]);
    //    }
    //}
});

gulp.task('default', ['All']);

//# sourceMappingURL=gulpfile.js.map