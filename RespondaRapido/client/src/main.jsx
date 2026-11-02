import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './style.css';
const money = v => new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL',maximumFractionDigits:0}).format(v||0);
const letters=['A','B','C','D'];
function useHostSocket(setState) {
 const [connected,setConnected]=useState(false),[error,setError]=useState('');
 useEffect(()=>{const p=location.protocol==='https:'?'wss:':'ws:';const ws=new WebSocket(`${p}//${location.host}/ws`);
 ws.onopen=()=>{setConnected(true);ws.send(JSON.stringify({type:'host'}))};
 ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.type==='state')setState(m.state);if(m.type==='rejected'||m.type==='notice')setError(m.message)};
 ws.onclose=()=>setConnected(false);window.sendGameMessage=m=>ws.readyState===WebSocket.OPEN&&ws.send(JSON.stringify(m));return()=>{delete window.sendGameMessage;ws.close()};
 },[]);
 return {connected,error};
}
function Brand({small=false}){return <div className={`brand ${small?'small':''}`}><span className="brand-mark">R</span><span>RESPOSTA <i>RÁPIDA</i></span></div>}
function Roster({players,showStatus=false}){return <ul className="roster">{players?.map(p=><li key={p.name} className={p.answered?'answered':''}><span className="avatar">{p.name[0]?.toUpperCase()}</span><strong>{p.name}</strong>{showStatus&&<span className={p.answered?'done':'waiting'}>{p.answered?'RESPONDEU':'PENSANDO'}</span>}</li>)}</ul>}
function Scores({state}){return <ol className="scores">{[...(state.players||[])].sort((a,b)=>b.balance-a.balance).map((p,i)=><li key={p.name}><span>{String(i+1).padStart(2,'0')}</span><strong>{p.name}</strong><b>{money(p.balance)}</b></li>)}</ol>}
function HostApp(){
 const [state,setState]=useState(null);const {connected,error}=useHostSocket(setState);
 const [selected,setSelected]=useState(null);
 useEffect(()=>{if(selected===null&&state?.subjects)setSelected(state.subjects.map(s=>s.code))},[state?.subjects,selected]);
 const send=m=>window.sendGameMessage?.(m);
 return <main className="host">
  <header><Brand small/><span className="connection"><i/>SALA AO VIVO</span></header>
  {!state||state.phase==='Lobby'?<section className="lobby">
   <article className="panel intro"><small>TELA DO ANFITRIÃO</small><h1>Vamos jogar?</h1><p>Escolha as matérias e reúna os participantes antes de começar.</p>
    <div className="join-link"><small>LINK PARA OS CELULARES</small><b>http://{location.host}/jogar</b></div>
    <h3>Matérias</h3><div className="subjects">{state?.subjects?.map(s=><label key={s.code}><input type="checkbox" checked={selected?.includes(s.code)||false} onChange={e=>setSelected(v=>e.target.checked?[...v,s.code]:v.filter(x=>x!==s.code))}/>{s.name}</label>)}</div>
    <button className="primary" disabled={!connected||!state?.players?.length} onClick={()=>send({type:'start',subjects:selected||[]})}>Iniciar partida <span>→</span></button>
   </article>
   <aside className="panel people"><div className="section-heading"><div><small>PARTICIPANTES</small><h2>{state?.players?.length||0} na sala</h2></div><span className="badge">{state?.players?.length||0}</span></div>
    {state?.players?.length?<Roster players={state.players}/>:<div className="empty"><span>▯</span><b>Aguardando jogadores</b><small>Abra o link no celular para entrar.</small></div>}
   </aside>
  </section>:<><div className="meta"><span>PERGUNTA <b>{state.questionNumber}</b><i> / {state.totalQuestions}</i></span><span className="prize">EM JOGO <b>{money(state.prize)}</b></span></div>
   {state.phase==='Finished'?<section className="panel finished"><small>FIM DE JOGO</small><h1>Grande partida!</h1><p>Saldo final dos participantes</p><Scores state={state}/></section>:
   state.phase==='QuestionClosed'?<section className="result">
    <article className="panel question"><small>RESPOSTA CERTA</small><h1>{state.question}</h1><div className="reveal"><b>{letters[state.correctOption]}</b><span>{state.options?.[state.correctOption]}</span><i>✓</i></div><button className="primary" onClick={()=>send({type:'next'})}>{state.questionNumber===state.totalQuestions?'Encerrar partida':'Próxima pergunta'} <span>→</span></button></article>
    <aside className="panel people"><small>SALDO DOS JOGADORES</small><Scores state={state}/></aside>
   </section>:<section className="game">
    <article className="panel question"><div className="question-heading"><small>PERGUNTA {state.questionNumber} DE {state.totalQuestions}</small><Countdown deadline={state.deadline}/></div><h1>{state.question}</h1><div className="answers">{state.options?.map((o,i)=><div key={i}><b>{letters[i]}</b><span>{o}</span></div>)}</div></article>
    <aside className="panel people"><div className="section-heading"><div><small>RESPOSTAS</small><h2>{state.players.filter(p=>p.answered).length}<i> / {state.players.length}</i></h2></div><span className="pulse"/></div><Roster players={state.players} showStatus/></aside>
   </section>}
  </>}
  <footer><span>REDE LOCAL</span><span>{connected?'Conectado':'Tela desconectada'}</span></footer>{error&&<div className="toast">{error}</div>}
 </main>;
}
function Countdown({deadline}){const [left,setLeft]=useState(20);useEffect(()=>{const tick=()=>setLeft(Math.max(0,Math.ceil((new Date(deadline)-Date.now())/1000)));tick();const id=setInterval(tick,150);return()=>clearInterval(id)},[deadline]);return <div className={`countdown ${left<6?'urgent':''}`}><b>{left}</b><span>seg</span></div>}
function PlayerApp(){
 const [name,setName]=useState(''),[joined,setJoined]=useState(false),[state,setState]=useState(null),[error,setError]=useState(''),[socket,setSocket]=useState(null);
 useEffect(()=>{if(!joined)return;const p=location.protocol==='https:'?'wss:':'ws:';const ws=new WebSocket(`${p}//${location.host}/ws`);setSocket(ws);ws.onopen=()=>ws.send(JSON.stringify({type:'join',name}));
 ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.type==='state')setState(m.state);if(m.type==='rejected'||m.type==='notice')setError(m.message)};ws.onclose=()=>setError('Conexão encerrada.');return()=>ws.close()},[joined]);
 const self=state?.players?.find(p=>p.name.toLocaleLowerCase()===name.trim().toLocaleLowerCase());
 const active=state?.phase==='QuestionOpen'&&state.question&&!self?.answered;
 if(!joined)return <main className="phone"><Brand/><section className="join panel"><small>ENTRAR NA PARTIDA</small><h1>Qual é o seu nome?</h1><form onSubmit={e=>{e.preventDefault();setJoined(true)}}><input value={name} maxLength="24" onChange={e=>setName(e.target.value)} placeholder="Seu nome"/><button className="primary" disabled={name.trim().length<2}>Entrar na sala <span>→</span></button></form>{error&&<p className="error">{error}</p>}</section><footer>RESPOSTA RÁPIDA · JOGO EM GRUPO</footer></main>;
 if(!state)return <main className="phone waiting-page"><Brand/><div className="spinner"/><p>{error||'Conectando à partida…'}</p></main>;
 return <main className="phone"><header className="player-header"><Brand small/><span>{name}</span></header>
  {active?<section className="mobile-question"><small>SUA RESPOSTA</small><h1>{state.question}</h1><div className="mobile-answers">{state.options.map((o,i)=><button key={i} onClick={()=>socket?.readyState===WebSocket.OPEN&&socket.send(JSON.stringify({type:'answer',option:i}))}><b>{letters[i]}</b>{o}</button>)}</div></section>:
  <section className="rest"><div className="rest-logo">R</div><Brand/><h2>{state.phase==='Lobby'?'Você está na sala':state.phase==='Finished'?'Partida encerrada':self?.answered?'Resposta registrada!':'Aguarde a próxima pergunta'}</h2><p>{state.phase==='Lobby'?'A partida começa em breve.':self?.answered?'Acompanhe o resultado na tela principal.':'Acompanhe a partida na tela principal.'}</p></section>}
  <footer>RESPOSTA RÁPIDA · JOGO EM GRUPO</footer>
 </main>;
}
createRoot(document.getElementById('root')).render(location.pathname==='/jogar'?<PlayerApp/>:<HostApp/>);