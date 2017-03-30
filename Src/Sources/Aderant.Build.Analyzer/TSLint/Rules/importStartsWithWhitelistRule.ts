import * as utils from "tsutils";
import * as ts from "typescript";
import * as Lint from "tslint";

export class Rule extends Lint.Rules.AbstractRule {
    /* tslint:disable:object-literal-sort-keys */
    public static metadata: Lint.IRuleMetadata = {
        ruleName: "import-blacklist",
        description: Lint.Utils.dedent`
            Disallows importing the modules without having a specified prefix.
            Instead all imports must be prefixed with their module name.`,
        optionsDescription: "A list of whitelist web modules.",
        options: {
            type: "array",
            items: {
                type: "string",
            },
            minLength: 1,
        },
        optionExamples: ["true", '[true, "rxjs", "lodash"]'],
        type: "functionality",
        typescriptOnly: false,
    };

    public static FAILURE_STRING = "This import needs to contain the Module name, eg WebCore, WebInquiries";

    public isEnabled(): boolean {
        const ruleArguments = this.getOptions().ruleArguments;
        return ruleArguments.length > 0;
    }

    public apply(sourceFile: ts.SourceFile): Lint.RuleFailure[] {
        return this.applyWithWalker(new ImportStartsWithWhitelistWalker(sourceFile, this.getOptions()));
    }
}

class ImportStartsWithWhitelistWalker extends Lint.RuleWalker {
    public visitCallExpression(node: ts.CallExpression) {
        if (node.expression.kind === ts.SyntaxKind.Identifier &&
            (node.expression as ts.Identifier).text === "require" &&
            node.arguments !== undefined &&
            node.arguments.length === 1) {

            this.checkForBannedImport(node.arguments[0]);
        }
        super.visitCallExpression(node);
    }

    public visitImportEqualsDeclaration(node: ts.ImportEqualsDeclaration) {
        if (utils.isExternalModuleReference(node.moduleReference) &&
            node.moduleReference.expression !== undefined) {
            // If it's an import require and not an import alias
            this.checkForBannedImport(node.moduleReference.expression);
        }
        super.visitImportEqualsDeclaration(node);
    }

    public visitImportDeclaration(node: ts.ImportDeclaration) {
        this.checkForBannedImport(node.moduleSpecifier);
        super.visitImportDeclaration(node);
    }

    private testOptions(expression: ts.LiteralExpression):boolean{
    let returnValue = false;
    for (let option of this.getOptions()){
            returnValue = returnValue || expression.text.indexOf(option) === 0;
        }
        return returnValue;
    }

    private checkForBannedImport(expression: ts.Expression) {
        if (utils.isTextualLiteral(expression) && !(this.testOptions(expression))) {
            
            this.addFailureFromStartToEnd(
                expression.getStart(this.getSourceFile()) + 1,
                expression.getEnd() - 1,
                Rule.FAILURE_STRING,
            );
        }
    }
}