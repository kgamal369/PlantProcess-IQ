import{a as e,n as t}from"./chunk-DnJy8xQt.js";import{o as n,t as r}from"./iframe-Bm-vu0Mc.js";import{t as i}from"./jsx-runtime-DxP0NviS.js";import{p as a,r as o}from"./StandardButton-B2Y-eCAT.js";import{i as s,n as c,r as l,t as u}from"./StandardFields-pHlrpvwo.js";var d,f,p,m,h,g,_;t((()=>{d=e(n(),1),o(),s(),r(),f=i(),p={title:`PlantProcess IQ/Standard/Fields`,parameters:{layout:`fullscreen`}},m={render:()=>{let[e,t]=(0,d.useState)(`COIL-1001`);return(0,f.jsx)(`div`,{className:`ppiq-std-standards-page`,children:(0,f.jsxs)(`div`,{style:{display:`grid`,gap:16,maxWidth:520},children:[(0,f.jsx)(u,{label:`Connector name`,required:!0,placeholder:`Production source`,leadingIcon:(0,f.jsx)(a,{size:16})}),(0,f.jsx)(u,{label:`Search material code`,type:`search`,value:e,onChange:t,helperText:`Canonical migration target for PPIQ-T025.`}),(0,f.jsx)(u,{label:`Error example`,error:`Connector name is required.`}),(0,f.jsx)(u,{label:`Loading example`,isLoading:!0,placeholder:`Refreshing...`})]})})}},h={render:()=>{let[e,t]=(0,d.useState)(`thermal`),[n,r]=(0,d.useState)([`thermal`]);return(0,f.jsx)(`div`,{className:`ppiq-std-standards-page`,children:(0,f.jsxs)(`div`,{style:{display:`grid`,gap:16,maxWidth:520},children:[(0,f.jsx)(c,{label:`Process domain`,value:e,onChange:t,searchable:!0,options:[{value:`thermal`,label:`Thermal process`},{value:`mechanical`,label:`Mechanical process`},{value:`inspection`,label:`Inspection / quality`}]}),(0,f.jsx)(c,{label:`Multi-select domains`,multiple:!0,value:n,onChange:r,searchable:!0,options:[{value:`thermal`,label:`Thermal process`},{value:`mechanical`,label:`Mechanical process`},{value:`inspection`,label:`Inspection / quality`}]})]})})}},g={render:()=>(0,f.jsx)(`div`,{className:`ppiq-std-standards-page`,children:(0,f.jsx)(`div`,{style:{maxWidth:520},children:(0,f.jsx)(l,{label:`Investigation note`,helperText:`Keep notes factual and avoid guaranteed root-cause wording.`})})})},m.parameters={...m.parameters,docs:{...m.parameters?.docs,source:{originalSource:`{
  render: () => {
    const [search, setSearch] = useState("COIL-1001");
    return <div className="ppiq-std-standards-page">\r
        <div style={{
        display: "grid",
        gap: 16,
        maxWidth: 520
      }}>\r
          <StandardInput label="Connector name" required placeholder="Production source" leadingIcon={<Database size={16} />} />\r
          <StandardInput label="Search material code" type="search" value={search} onChange={setSearch} helperText="Canonical migration target for PPIQ-T025." />\r
          <StandardInput label="Error example" error="Connector name is required." />\r
          <StandardInput label="Loading example" isLoading placeholder="Refreshing..." />\r
        </div>\r
      </div>;
  }
}`,...m.parameters?.docs?.source}}},h.parameters={...h.parameters,docs:{...h.parameters?.docs,source:{originalSource:`{
  render: () => {
    const [single, setSingle] = useState<string | string[]>("thermal");
    const [multi, setMulti] = useState<string | string[]>(["thermal"]);
    return <div className="ppiq-std-standards-page">\r
        <div style={{
        display: "grid",
        gap: 16,
        maxWidth: 520
      }}>\r
          <StandardSelect label="Process domain" value={single} onChange={setSingle} searchable options={[{
          value: "thermal",
          label: "Thermal process"
        }, {
          value: "mechanical",
          label: "Mechanical process"
        }, {
          value: "inspection",
          label: "Inspection / quality"
        }]} />\r
          <StandardSelect label="Multi-select domains" multiple value={multi} onChange={setMulti} searchable options={[{
          value: "thermal",
          label: "Thermal process"
        }, {
          value: "mechanical",
          label: "Mechanical process"
        }, {
          value: "inspection",
          label: "Inspection / quality"
        }]} />\r
        </div>\r
      </div>;
  }
}`,...h.parameters?.docs?.source}}},g.parameters={...g.parameters,docs:{...g.parameters?.docs,source:{originalSource:`{
  render: () => <div className="ppiq-std-standards-page">\r
      <div style={{
      maxWidth: 520
    }}>\r
        <StandardTextArea label="Investigation note" helperText="Keep notes factual and avoid guaranteed root-cause wording." />\r
      </div>\r
    </div>
}`,...g.parameters?.docs?.source}}},_=[`Inputs`,`Selects`,`TextArea`]}))();export{m as Inputs,h as Selects,g as TextArea,_ as __namedExportsOrder,p as default};