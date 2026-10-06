// TypeScript is already locked by the frontend. No fallback text scanner is permitted.
const fs = require('node:fs');
const path = require('node:path');
const ts = require('../../frontend/node_modules/typescript');
if (ts.version !== '5.9.2') throw new Error(`ParserVersionMismatch:${ts.version}`);
const rawFields = new Set(['alarmBits', 'alarmSeverity', 'protocolStatus', 'inspectionStatus',
  'zResetStatus', 'flipStatus', 'palletLockStatus', 'plcSystemFault', 'reliableFeedback',
  'sortingAckCleared', 'documentNumber', 'pduOffset', 'registerAddress', 'rawWords', 'rawBytes',
  'positionEvidence', 'SortingAckCleared', 'ProtocolStatus', 'PositionEvidence',
  'plcWriteAudit', 'plcAudit', 'plcChanges', 'plcState']);
// The rule vocabulary is frozen configuration; no source file is exempted.
const handshakeFacts=new Set(require('./009-script-boundary-cases.json').internalHandshakeIdentities);
const deviceFields = new Set(['schemaVersion','reliability','connection','connectionEpoch','operatingMode',
  'readiness','safetyAssessment','clamp','motionAvailability','acquisitionReadiness','manualArea',
  'manualHandling','position','face','alarms','reasonCodes','executionOrigin','observationId',
  'sampleStartedUtc','sampleEndedUtc','diagnosticEvidenceReference','axisObservations']);
const diagnosticFields = new Set(['schemaVersion','safetyAssessment','reasonCodes','stopStage','disposition',
  'semanticObservation','executionOrigin','diagnosticEvidenceReference','connectionEpoch',
    'observedAtUtc','recordNature','rawAvailability']);
const completionFields = new Set(['finalOutcome','receiptValidity','canContinue','approvalValid']);

function check(input) {
  const file = ts.createSourceFile(input.path, input.source, ts.ScriptTarget.Latest, true,
    input.path.endsWith('.ts') ? ts.ScriptKind.TS : ts.ScriptKind.JS);
  const options={allowJs:true,noEmit:true,noLib:true,noResolve:true,target:ts.ScriptTarget.Latest};
  const host=ts.createCompilerHost(options);
  host.getSourceFile=name=>name===input.path ? file : undefined;
  host.fileExists=name=>name===input.path;
  host.readFile=name=>name===input.path ? input.source : undefined;
  const program=ts.createProgram([input.path],options,host);
  const checker=program.getTypeChecker();
  const identity=n=>checker.getSymbolAtLocation(n) || n;
  const violations = [], errors = [], dependencies = [], seen = new Set(), aliases = new Set(), schemas = new Map();
  const communication = ['communication-probe','communication-device'].includes(input.role);
  const position = n => { const p = file.getLineAndCharacterOfPosition(n.getStart(file)); return {line:p.line+1, column:p.character+1}; };
  const reject = (ruleId,n,message) => {
    const key = `${ruleId}:${n.pos}`; if (seen.has(key)) return; seen.add(key);
    violations.push({ruleId,path:input.path,...position(n),sourcePath:input.path,message});
  };
  for (const d of file.parseDiagnostics) errors.push({code:'PARSE',path:input.path,
    message:ts.flattenDiagnosticMessageText(d.messageText,' '),start:d.start});
  if (errors.length) return {parserVersion:ts.version,parsed:false,violations,errors,dependencies};
  const children = n => { const out=[]; ts.forEachChild(n,c=>{out.push(c);}); return out; };
  const member = n => ts.isPropertyAccessExpression(n) ? n.name.text : ts.isElementAccessExpression(n) &&
    ts.isStringLiteralLike(n.argumentExpression) ? n.argumentExpression.text : null;
  const object = n => ts.isPropertyAccessExpression(n) || ts.isElementAccessExpression(n) ? n.expression : null;
  function schema(n) {
    if(!n) return null;
    if(ts.isIdentifier(n)) return schemas.get(identity(n));
    const field=member(n);
    if(['plc','semanticObservation'].includes(field)) return 'device-semantics/1';
    if(field==='startupDiagnostic') return 'startup-diagnostic/1';
    return null;
  }
  function mergeSchema(symbol,kind) {
    const old=schemas.get(symbol);
    if(old===kind || old==='ambiguous') return false;
    schemas.set(symbol,old ? 'ambiguous' : kind); return true;
  }
  function raw(n) {
    if (!n) return false;
    if (ts.isIdentifier(n) && aliases.has(identity(n))) return true;
    if (rawFields.has(member(n))) return true;
    if (ts.isCallExpression(n) && n.arguments.some(a=>ts.isStringLiteralLike(a) && rawFields.has(a.text))) return true;
    return children(n).some(raw);
  }
  function bind(name) {
    if (ts.isIdentifier(name)) { const size=aliases.size; aliases.add(identity(name)); return aliases.size!==size; }
    if (ts.isObjectBindingPattern(name) || ts.isArrayBindingPattern(name)) {
      let changed=false; for(const e of name.elements) if(ts.isBindingElement(e)) changed=bind(e.name)||changed; return changed;
    }
    return false;
  }
  let changed;
  do {
    changed=false;
    function propagate(n) {
      if (ts.isVariableDeclaration(n) && raw(n.initializer)) changed=bind(n.name)||changed;
      if (ts.isVariableDeclaration(n) && ts.isIdentifier(n.name) && schema(n.initializer) &&
          schemas.get(identity(n.name))!==schema(n.initializer)) {
        changed=mergeSchema(identity(n.name),schema(n.initializer))||changed;
      }
      if (ts.isBinaryExpression(n) && n.operatorToken.kind===ts.SyntaxKind.EqualsToken && raw(n.right)) changed=bind(n.left)||changed;
      if (ts.isFunctionDeclaration(n) && n.name && n.body && raw(n.body)) changed=bind(n.name)||changed;
      if (ts.isCallExpression(n)) {
        const symbol=checker.getSymbolAtLocation(n.expression);
        const declaration=symbol?.valueDeclaration;
        if (declaration && ts.isFunctionDeclaration(declaration))
          for(let i=0;i<Math.min(declaration.parameters.length,n.arguments.length);i++) {
            if(raw(n.arguments[i])) changed=bind(declaration.parameters[i].name)||changed;
            const parameter=declaration.parameters[i].name, kind=schema(n.arguments[i]);
            if(kind && ts.isIdentifier(parameter)) changed=mergeSchema(identity(parameter),kind)||changed;
          }
      }
      ts.forEachChild(n,propagate);
    }
    propagate(file);
  } while(changed);
  function visit(n) {
    if (!communication) {
      if(ts.isStringLiteralLike(n)&&handshakeFacts.has(n.text))
        reject("A08",n,"Known internal handshake identity in protected business assertion.");
      const inputSchema=schema(object(n)), field=member(n);
      if(inputSchema==='ambiguous') reject('A09',n,'Conflicting device schema sources require an explicit boundary.');
      if(inputSchema && field && !(inputSchema==='device-semantics/1'?deviceFields:diagnosticFields).has(field) && !rawFields.has(field))
        reject('A10',n,`Unregistered field ${field} in ${inputSchema}; confirm the business contract before use.`);
      if(inputSchema && ts.isElementAccessExpression(n) && !ts.isStringLiteralLike(n.argumentExpression))
        reject('A09',n,'Dynamic key on a protected device/diagnostic object.');
      if(ts.isBindingElement(n) && rawFields.has(n.propertyName?.getText(file) || n.name.getText(file)))
        reject('A08',n,'Destructuring a raw field does not make it a semantic value.');
      if (rawFields.has(member(n))) reject('A08',n,'Raw protocol field accessed from protected script.');
      if (ts.isBinaryExpression(n) && raw(n) && n.operatorToken.kind!==ts.SyntaxKind.EqualsToken)
        reject('A08',n,'Raw comparison/bit operation remains protocol knowledge after aliasing.');
      if (ts.isElementAccessExpression(n) && raw(n.expression) && !ts.isStringLiteralLike(n.argumentExpression))
        reject('A09',n,'Dynamic access to protected raw object is unsupported.');
      if (ts.isCallExpression(n) && n.arguments.some(raw)) {
        const name=n.expression.getText(file);
        if (!['JSON.stringify','console.log','console.error','process.stdout.write','fs.writeFileSync'].includes(name))
          reject('A09',n,'Protected raw data escapes through a helper/call.');
      }
      if(ts.isCallExpression(n) && n.arguments.some(a=>schema(a))) {
        const target=checker.getSymbolAtLocation(n.expression)?.valueDeclaration;
        if(!(target && ts.isFunctionDeclaration(target)) && !['JSON.stringify','console.log','console.error'].includes(n.expression.getText(file)))
          reject('A09',n,'Semantic device object escapes to an unmodelled helper; provide an explicit local/schema boundary.');
      }
      if (ts.isCallExpression(n) && ['eval','Function','Reflect.get'].includes(n.expression.getText(file)))
        reject('A09',n,'Dynamic evaluation/reflection is outside the finite business assertion boundary.');
    }
    if(input.role==='communication-probe' && completionFields.has(member(n)))
      reject('A10',n,'Communication probe contains a business completion/approval assertion.');
    if (ts.isImportDeclaration(n) && ts.isStringLiteral(n.moduleSpecifier)) dependencies.push(n.moduleSpecifier.text);
    if (ts.isExportDeclaration(n) && n.moduleSpecifier && ts.isStringLiteral(n.moduleSpecifier)) dependencies.push(n.moduleSpecifier.text);
    if (ts.isCallExpression(n) && (n.expression.getText(file)==='require' || n.expression.kind===ts.SyntaxKind.ImportKeyword)) {
      if(n.arguments.length===1 && ts.isStringLiteralLike(n.arguments[0])) dependencies.push(n.arguments[0].text);
      else reject('A10',n,'Dynamic local module cannot be classified.');
    }
    ts.forEachChild(n,visit);
  }
  visit(file);
  return {parserVersion:ts.version,parsed:true,violations,errors,dependencies};
}
module.exports={check};
if (require.main===module) {
  const input=JSON.parse(fs.readFileSync(0,'utf8'));
  process.stdout.write(JSON.stringify(input.map(check)));
}
