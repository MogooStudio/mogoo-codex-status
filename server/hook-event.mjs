export function hookEvent(input) {
  if(input?.hook_event_name==='UserPromptSubmit')return {event:'UserPromptSubmit',source:null};
  if(input?.hook_event_name==='SessionStart'&&['startup','resume'].includes(input.source))return {event:'SessionStart',source:input.source};
  return null;
}
