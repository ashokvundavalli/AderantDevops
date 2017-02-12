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
			dependentModules.push("Web.Foundation", "Web.Presentation", "Web.OTG");
        case "web.workflow":
			dependentModules.push("Web.Foundation", "Web.Presentation", "Web.OTG");
        case "web.expenses":
			dependentModules.push("Web.Foundation", "Web.Presentation", "Web.OTG");
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

gulp.task('WatchWeb.Foundation', ['setModule', 'Web.Foundation'], function (modulesPath) {
	copyModule.setModulesPath(modulesPath);
    copyModule.watchAndCopyChanges('Web.Foundation');
});

gulp.task('Web.Foundation', function (moduleName, modulesPath, watch) {
    copyModule.setCurrentModule(moduleName);
	copyModule.setModulesPath(modulesPath);

    console.log("Copying dependencies from Web.Foundation -> " + moduleName);
    copyModule.copy('Web.Foundation');
});

gulp.task('WatchWeb.Presentation', ['setModule', 'Web.Presentation'], function (modulesPath) {
	copyModule.setModulesPath(modulesPath);
    copyModule.watchAndCopyChanges('Web.Presentation');
});

gulp.task('Web.Presentation', function (moduleName, modulesPath, watch) {
    copyModule.setCurrentModule(moduleName);
	copyModule.setModulesPath(modulesPath);
    console.log("Copying dependencies from Web.Presentation -> " + moduleName);
    copyModule.copy('Web.Presentation');
});

gulp.task('WatchWeb.OTG', ['setModule', 'Web.OTG'], function (modulesPath) {
	copyModule.setModulesPath(modulesPath);
    copyModule.watchAndCopyChanges('Web.OTG');
});

gulp.task('Web.OTG', function (moduleName, modulesPath, watch) {
    copyModule.setCurrentModule(moduleName);
	copyModule.setModulesPath(modulesPath);
    console.log("Copying dependencies from Web.OTG -> " + moduleName);
    copyModule.copy('Web.OTG');
});

gulp.task('WatchWeb.SMB', ['setModule', 'Web.SMB'], function (modulesPath) {
	copyModule.setModulesPath(modulesPath);
    copyModule.watchAndCopyChanges('Web.SMB');
});

gulp.task('Web.SMB', function (moduleName, modulesPath, watch) {
    copyModule.setCurrentModule(moduleName);
	copyModule.setModulesPath(modulesPath);
    console.log("Copying dependencies from Web.SMB -> " + moduleName);
    copyModule.copy('Web.SMB');
});

gulp.task('WatchAll', ['setModule', 'All'], function (modulesPath) {
	copyModule.setModulesPath(modulesPath);
    for (var i = 0; i < dependentModules.length; i++) {
        copyModule.watchAndCopyChanges(dependentModules[i]);
    }
});

gulp.task('All', ['setModule'], function (modulesPath,watch) {
	copyModule.setModulesPath(modulesPath);
    for (var i = 0; i < dependentModules.length; i++) {
	    console.log("Copying dependencies from " + dependentModules[i] + " -> " + currentModule);
	    copyModule.copy(dependentModules[i]);
	}
});

gulp.task('default', ['All']);

//# sourceMappingURL=gulpfile.js.map