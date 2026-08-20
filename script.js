
// const options = {
//   method: 'GET',
//   headers: {
//     accept: 'application/json',
//     authorization: 'Bearer 52a773a7-189f-7a69-2d75-68c24af1f715'
//   }
// };





//   fetch('https://apisandbox.dev.clover.com/v3/merchants/PDW4DZ6RNQPA1/orders', options)
//     .then(res => res.json())
//     .then(res => console.log(res))
//     .catch(err => console.error(err));




  
  


  
async function fetchData() {
  try {
    
    const options = {
      method: 'GET',
      headers: {
        accept: 'application/json',
        authorization: 'Bearer 52a773a7-189f-7a69-2d75-68c24af1f715'
      }
    };

    const response = await fetch('https://apisandbox.dev.clover.com/v3/merchants/PDW4DZ6RNQPA1/items', options)
      .then(res => res.json())
      .then(res => console.log(res))
      .catch(err => console.error(err));


    
    if (!response.ok) {
      throw new Error('HTTP error, Status: ${response.status}');
    }

    const data = await response.json();
    return data;
    
  } catch (error) {
    console.error('Error retrieving data:', error);
    return []; // return empty array when error occurs
  }

}


// const menuItem = document.getElementById('menu-item');

// fetchData().then()
