create table operadoras (
    registro_ans integer primary key,
    cnpj varchar(20),
    razao_social varchar(255),
    nome_fantasia varchar(255),
    modalidade varchar(50),
    logradouro varchar(255),
    numero varchar(20),
    complemento varchar(100),
    bairro varchar(100),
    cidade varchar(100),
    uf varchar(2),
    cep varchar(10),
    ddd varchar(10),
    telefone varchar(20),
    fax varchar(20),
    endereco_eletronico varchar(255),
    representante varchar(255),
    cargo_representante varchar(100),
    regiao_de_comercializacao varchar(100),
    data_registro_ans date
);

create table demonstracoes_contabeis (
    id serial primary key,
    data date,
    reg_ans integer,
    cd_conta_contabil varchar(50),
    descricao varchar(255),
    vl_saldo_inicial numeric(15,2),
    vl_saldo_final numeric(15,2),
    constraint fk_operadora
      foreign key (reg_ans) references operadoras(codigo_operadora)
);

COPY operadoras(registro_ans, cnpj, razao_social, nome_fantasia, modalidade,
    logradouro, numero, complemento, bairro, cidade, uf, cep,
    ddd, telefone, fax, endereco_eletronico, representante,
    cargo_representante, regiao_de_comercializacao, data_registro_ans)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/dados_cadastrais/Relatorio_cadop.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);

COPY demonstracoes_contabeis(data, reg_ans, cd_conta_contabil, descricao, vl_saldo_inicial, vl_saldo_final)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/demonstrativos/1T2023.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);
COPY demonstracoes_contabeis(data, reg_ans, cd_conta_contabil, descricao, vl_saldo_inicial, vl_saldo_final)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/demonstrativos/2t2023.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);
COPY demonstracoes_contabeis(data, reg_ans, cd_conta_contabil, descricao, vl_saldo_inicial, vl_saldo_final)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/demonstrativos/3T2023.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);
COPY demonstracoes_contabeis(data, reg_ans, cd_conta_contabil, descricao, vl_saldo_inicial, vl_saldo_final)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/demonstrativos/4T2023.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);
COPY demonstracoes_contabeis(data, reg_ans, cd_conta_contabil, descricao, vl_saldo_inicial, vl_saldo_final)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/demonstrativos/1T2024.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);
COPY demonstracoes_contabeis(data, reg_ans, cd_conta_contabil, descricao, vl_saldo_inicial, vl_saldo_final)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/demonstrativos/2T2024.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);
COPY demonstracoes_contabeis(data, reg_ans, cd_conta_contabil, descricao, vl_saldo_inicial, vl_saldo_final)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/demonstrativos/3T2024.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);
COPY demonstracoes_contabeis(data, reg_ans, cd_conta_contabil, descricao, vl_saldo_inicial, vl_saldo_final)
FROM '/home/valdevino/Downloads/intuitivecare/intuitivecare/database/demonstrativos/4T2024.csv'
with (
    FORMAT csv,
    HEADER true,
    ENCODING 'LATIN1'
);

         --10 operadoras com maiores despesas em "EVENTOS/ SINISTROS CONHECIDOS OU
         --AVISADOS DE ASSISTÊNCIA A SAÚDE MEDICO HOSPITALAR" no último trimestre?
with periodo as (
    select
        (date_trunc('quarter', current_date) - interval '3 month')::date as inicio,
        date_trunc('quarter', current_date)::date as fim
)
select
    o.nome_fantasia,
    sum(dc.vl_saldo_inicial - dc.vl_saldo_final) as total_despesa
from demonstracoes_contabeis dc
join operadoras o
  on dc.reg_ans = o.registro_ans
join periodo p on true
where dc.descricao = 'EVENTOS/ SINISTROS CONHECIDOS OU AVISADOS DE ASSISTÊNCIA A SAÚDE MEDICO HOSPITALAR'
  and dc.data >= p.inicio
  and dc.data < p.fim
group by o.nome_fantasia
order by total_despesa desc
limit 10;

        --Quais as 10 operadoras com maiores despesas nessa categoria no último ano?
WITH periodo AS (
    SELECT
        (current_date - interval '1 year')::date AS inicio,
        current_date AS fim
)
SELECT
    o.nome_fantasia,
    SUM(dc.vl_saldo_inicial - dc.vl_saldo_final) AS total_despesa
FROM demonstracoes_contabeis dc
JOIN operadoras o
  ON dc.reg_ans = o.registro_ans
JOIN periodo p ON true
WHERE dc.descricao = 'EVENTOS/ SINISTROS CONHECIDOS OU AVISADOS DE ASSISTÊNCIA A SAÚDE MEDICO HOSPITALAR'
  AND dc.data >= p.inicio
  AND dc.data < p.fim
GROUP BY o.nome_fantasia
ORDER BY total_despesa DESC
LIMIT 10;
