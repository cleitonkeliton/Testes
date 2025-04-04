from flask import Flask, request, jsonify
from flask_cors import CORS
import pandas as pd

app = Flask(__name__)
CORS(app)

df = pd.read_csv(
    '/home/valdevino/Downloads/intuitivecare/api/dados_cadastrais/Relatorio_cadop.csv',
    sep=';',
    encoding='iso-8859-1'  # Ou 'latin-1' se houver erros
)

@app.route('/search', methods=['GET'])
def search():
    param_map = {
        's': 'search_all',
        'a': 'Razao_Social',
        'e': 'Nome_Fantasia',
        'i': 'Cidade',
        'o': 'UF',
        'u': 'CNPJ',
        'q': 'Telefone'
    }

    filters = {}
    for param, column in param_map.items():
        value = request.args.get(param, '').lower()
        if value:
            filters[column] = value

    mask = pd.Series(True, index=df.index)

    for column, value in filters.items():
        if column == 'search_all':
            # Busca geral nas colunas textuais principais
            text_columns = ['Razao_Social', 'Nome_Fantasia', 'Cidade', 'UF']
            mask &= df[text_columns].apply(
                lambda col: col.astype(str).str.lower().str.contains(value, na=False)
            ).any(axis=1)
        else:
            mask &= df[column].astype(str).str.lower().str.contains(value, na=False)

    results = df[mask].head(20).to_dict(orient='records')
    return jsonify(results)

if __name__ == '__main__':
    app.run(debug=True, port=5001)